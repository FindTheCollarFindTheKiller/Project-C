// Project C.cpp : Defines the entry point for the application.
//

#include "framework.h"
#include "Project C.h"

#include <winhttp.h>
#include <string>
#include <vector>
#include <cstdlib>
#include <iostream>
#include <fstream>
#include <algorithm>
#include <cctype>
#include <thread>
#include <atomic>
#include <sstream>
#include <memory>
#include <cstdio>

#pragma comment(lib, "winhttp.lib")

#define MAX_LOADSTRING 100

// Global Variables:
HINSTANCE hInst;                                // current instance
WCHAR szTitle[MAX_LOADSTRING];                  // The title bar text
WCHAR szWindowClass[MAX_LOADSTRING];            // the main window class name

constexpr UINT WM_APP_GENERATION_COMPLETE = WM_APP + 1;

HWND gScriptLabel = nullptr;
HWND gScriptInput = nullptr;
HWND gProviderLabel = nullptr;
HWND gProviderCombo = nullptr;
HWND gTaskLabel = nullptr;
HWND gTaskCombo = nullptr;
HWND gModelLabel = nullptr;
HWND gModelInput = nullptr;
HWND gGenerateButton = nullptr;
HWND gOutputLabel = nullptr;
HWND gOutputView = nullptr;
HWND gStatusLabel = nullptr;
std::atomic_bool gIsGenerating = false;

struct GenerationResult
{
    bool success = false;
    bool showSetupGuide = false;
    std::wstring output;
    std::wstring error;
};

// Forward declarations of functions included in this code module:
ATOM                MyRegisterClass(HINSTANCE hInstance);
BOOL                InitInstance(HINSTANCE, int);
LRESULT CALLBACK    WndProc(HWND, UINT, WPARAM, LPARAM);
INT_PTR CALLBACK    About(HWND, UINT, WPARAM, LPARAM);
void                CreateMainControls(HWND hWnd);
void                LayoutMainControls(HWND hWnd);
void                StartGenerationFromUi(HWND hWnd);
void                SetStatusText(const std::wstring& text);
std::wstring        GetControlText(HWND control);
void                ApplyDefaultModelForTask(int taskIndex);
std::wstring        BuildLocalSetupGuideText();
void                ShowLocalSetupGuideDialog(HWND hWnd);
bool                IsLocalDependencyMissingError(const std::wstring& error);
bool                IsMediaGenerationTask(int taskIndex);
std::wstring        BuildGenerationStatusText(int taskIndex);
bool                RunPythonPreflightCheck(int taskIndex, std::wstring& error);
bool                WriteUtf8File(const std::wstring& path, const std::string& content, std::wstring& error);
bool                ReadUtf8File(const std::wstring& path, std::string& content, std::wstring& error);
std::wstring        CreateTempFilePath(const std::wstring& prefix);
std::wstring        TaskNameFromIndex(int taskIndex);
bool                GenerateLocalTransformersResult(const std::wstring& script, int taskIndex, const std::wstring& model, std::wstring& output, std::wstring& error);

std::wstring Utf8ToWide(const std::string& value)
{
    if (value.empty())
    {
        return L"";
    }

    const int size = MultiByteToWideChar(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), nullptr, 0);
    if (size <= 0)
    {
        return L"";
    }

    std::wstring result(size, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), result.data(), size);
    return result;
}

std::string WideToUtf8(const std::wstring& value)
{
    if (value.empty())
    {
        return "";
    }

    const int size = WideCharToMultiByte(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), nullptr, 0, nullptr, nullptr);
    if (size <= 0)
    {
        return "";
    }

    std::string result(size, '\0');
    WideCharToMultiByte(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), result.data(), size, nullptr, nullptr);
    return result;
}

std::wstring EscapeJson(const std::wstring& value)
{
    std::wstring escaped;
    escaped.reserve(value.size() + 16);
    for (wchar_t ch : value)
    {
        switch (ch)
        {
        case L'\\': escaped += L"\\\\"; break;
        case L'\"': escaped += L"\\\""; break;
        case L'\n': escaped += L"\\n"; break;
        case L'\r': escaped += L"\\r"; break;
        case L'\t': escaped += L"\\t"; break;
        default: escaped += ch; break;
        }
    }
    return escaped;
}

std::wstring ReadEnvWide(const wchar_t* name)
{
    const DWORD required = GetEnvironmentVariableW(name, nullptr, 0);
    if (required == 0)
    {
        return L"";
    }

    std::wstring value(required - 1, L'\0');
    GetEnvironmentVariableW(name, value.data(), required);
    return value;
}

std::string TrimAscii(std::string value)
{
    value.erase(value.begin(), std::find_if(value.begin(), value.end(), [](unsigned char ch) { return !std::isspace(ch); }));
    value.erase(std::find_if(value.rbegin(), value.rend(), [](unsigned char ch) { return !std::isspace(ch); }).base(), value.end());
    return value;
}

void LoadDotEnvFileIfPresent()
{
    if (!ReadEnvWide(L"OLLAMA_API_KEY").empty())
    {
        return;
    }

    std::ifstream envFile(".env", std::ios::in | std::ios::binary);
    if (!envFile.is_open())
    {
        return;
    }

    std::string line;
    while (std::getline(envFile, line))
    {
        if (!line.empty() && line.back() == '\r')
        {
            line.pop_back();
        }

        const std::string trimmed = TrimAscii(line);
        if (trimmed.empty() || trimmed[0] == '#')
        {
            continue;
        }

        const size_t equalsPos = trimmed.find('=');
        if (equalsPos == std::string::npos)
        {
            continue;
        }

        const std::string key = TrimAscii(trimmed.substr(0, equalsPos));
        std::string value = TrimAscii(trimmed.substr(equalsPos + 1));

        if (value.size() >= 2 && ((value.front() == '"' && value.back() == '"') || (value.front() == '\'' && value.back() == '\'')))
        {
            value = value.substr(1, value.size() - 2);
        }

        if (!key.empty())
        {
            _putenv_s(key.c_str(), value.c_str());
        }
    }
}

std::string ExtractJsonStringField(const std::string& json, const std::string& fieldName)
{
    const std::string key = "\"" + fieldName + "\"";
    size_t keyPos = json.find(key);
    if (keyPos == std::string::npos)
    {
        return "";
    }

    size_t colonPos = json.find(':', keyPos + key.size());
    if (colonPos == std::string::npos)
    {
        return "";
    }

    size_t firstQuote = json.find('"', colonPos + 1);
    if (firstQuote == std::string::npos)
    {
        return "";
    }

    std::string extracted;
    bool escaping = false;
    for (size_t i = firstQuote + 1; i < json.size(); ++i)
    {
        const char c = json[i];
        if (escaping)
        {
            switch (c)
            {
            case 'n': extracted.push_back('\n'); break;
            case 'r': extracted.push_back('\r'); break;
            case 't': extracted.push_back('\t'); break;
            case '\\': extracted.push_back('\\'); break;
            case '"': extracted.push_back('"'); break;
            default: extracted.push_back(c); break;
            }
            escaping = false;
            continue;
        }

        if (c == '\\')
        {
            escaping = true;
            continue;
        }

        if (c == '"')
        {
            return extracted;
        }

        extracted.push_back(c);
    }

    return "";
}

std::string ExtractFirstAvailableField(const std::string& json, const std::vector<std::string>& fields)
{
    for (const auto& field : fields)
    {
        std::string value = ExtractJsonStringField(json, field);
        if (!value.empty())
        {
            return value;
        }
    }
    return "";
}

std::wstring BuildEmbeddingPreview(const std::string& json)
{
    const size_t firstOpen = json.find('[');
    const size_t firstClose = json.find(']', firstOpen == std::string::npos ? 0 : firstOpen + 1);
    if (firstOpen == std::string::npos || firstClose == std::string::npos || firstClose <= firstOpen)
    {
        return L"Could not parse embedding vector from Hugging Face response.";
    }

    std::string candidate = json.substr(firstOpen, firstClose - firstOpen + 1);
    if (candidate.size() > 280)
    {
        candidate = candidate.substr(0, 280) + " ...";
    }

    return L"Embedding preview: " + Utf8ToWide(candidate);
}

std::wstring BuildEntityPreview(const std::string& json)
{
    std::wstringstream stream;
    stream << L"Named entities:\n";

    size_t position = 0;
    int count = 0;
    while (count < 8)
    {
        const size_t wordKey = json.find("\"word\"", position);
        if (wordKey == std::string::npos)
        {
            break;
        }

        const size_t entityKey = json.find("\"entity_group\"", wordKey);
        const size_t fallbackEntityKey = json.find("\"entity\"", wordKey);
        const std::string word = ExtractJsonStringField(json.substr(wordKey), "word");
        std::string entity;
        if (entityKey != std::string::npos)
        {
            entity = ExtractJsonStringField(json.substr(entityKey), "entity_group");
        }
        else if (fallbackEntityKey != std::string::npos)
        {
            entity = ExtractJsonStringField(json.substr(fallbackEntityKey), "entity");
        }

        if (!word.empty())
        {
            stream << L"- " << Utf8ToWide(word);
            if (!entity.empty())
            {
                stream << L" (" << Utf8ToWide(entity) << L")";
            }
            stream << L"\n";
            ++count;
        }

        position = wordKey + 6;
    }

    if (count == 0)
    {
        return L"Could not parse entities from Hugging Face response.";
    }

    return stream.str();
}

bool WriteUtf8File(const std::wstring& path, const std::string& content, std::wstring& error)
{
    std::ofstream output(path, std::ios::binary | std::ios::trunc);
    if (!output.is_open())
    {
        error = L"Could not write temporary file: " + path;
        return false;
    }

    output.write(content.data(), static_cast<std::streamsize>(content.size()));
    return true;
}

bool ReadUtf8File(const std::wstring& path, std::string& content, std::wstring& error)
{
    std::ifstream input(path, std::ios::binary);
    if (!input.is_open())
    {
        error = L"Could not read temporary file: " + path;
        return false;
    }

    content.assign(std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>());
    return true;
}

std::wstring CreateTempFilePath(const std::wstring& prefix)
{
    wchar_t tempPath[MAX_PATH]{};
    if (GetTempPathW(MAX_PATH, tempPath) == 0)
    {
        return L"";
    }

    wchar_t tempFile[MAX_PATH]{};
    if (GetTempFileNameW(tempPath, prefix.c_str(), 0, tempFile) == 0)
    {
        return L"";
    }

    return std::wstring(tempFile);
}

std::wstring TaskNameFromIndex(int taskIndex)
{
    switch (taskIndex)
    {
    case 1:
        return L"summarization";
    case 2:
        return L"sentiment";
    case 3:
        return L"embeddings";
    case 4:
        return L"ner";
    case 5:
        return L"sitcom_script";
    case 6:
        return L"script_to_video";
    case 7:
        return L"script_to_audio";
    case 8:
        return L"script_to_episode";
    default:
        return L"scene_outline";
    }
}

bool IsMediaGenerationTask(int taskIndex)
{
    return taskIndex == 6 || taskIndex == 7 || taskIndex == 8;
}

std::wstring BuildGenerationStatusText(int taskIndex)
{
    if (taskIndex == 6)
    {
        return L"Generating video... this may take several minutes.";
    }

    if (taskIndex == 7)
    {
        return L"Generating audio... this may take up to a minute.";
    }

    if (taskIndex == 8)
    {
        return L"Generating full episode (video + audio)... this may take several minutes.";
    }

    return L"Generating... this may take a few seconds.";
}

std::wstring BuildLocalSetupGuideText()
{
    return
        L"Local Transformers Setup Guide\n\n"
        L"This mode runs fully on your machine using Python + Hugging Face libraries.\n\n"
        L"1) Install Python 3.13+ (and ensure 'python' or 'py -3.13' works).\n"
        L"2) Open a terminal in this project folder.\n"
        L"3) Run:\n"
        L"   powershell -ExecutionPolicy Bypass -File scripts/setup_local_transformers.ps1\n"
        L"   (or install manually with: pip install -r requirements.txt)\n\n"
        L"4) Retry generation from the app.\n\n"
        L"Tip: first run may download model files to local cache. Outputs are written to generated_videos/, generated_audio/, and generated_episodes/.";
}

void ShowLocalSetupGuideDialog(HWND hWnd)
{
    MessageBoxW(hWnd, BuildLocalSetupGuideText().c_str(), L"Local Transformers Setup", MB_OK | MB_ICONINFORMATION);
}

bool IsLocalDependencyMissingError(const std::wstring& error)
{
    return error.find(L"DEPENDENCY_PRECHECK_FAILED:") == 0;
}

bool RunPythonPreflightCheck(int taskIndex, std::wstring& error)
{
    std::wstring imports = L"transformers, sentence_transformers, torch, numpy";
    if (taskIndex == 6 || taskIndex == 8)
    {
        imports += L", diffusers, imageio, imageio_ffmpeg";
    }

    const std::wstring pythonCommand = L"python -c \"import sys; raise SystemExit(0 if sys.version_info >= (3, 13) else 1)\" >nul 2>&1";
    if (_wsystem(pythonCommand.c_str()) == 0)
    {
        const std::wstring pythonImportCommand = L"python -c \"import " + imports + L"\" >nul 2>&1";
        if (_wsystem(pythonImportCommand.c_str()) == 0)
        {
            return true;
        }
    }

    const std::wstring pyCommand = L"py -3.13 -c \"import " + imports + L"\" >nul 2>&1";
    const int pyImportCheck = _wsystem(pyCommand.c_str());
    if (pyImportCheck == 0)
    {
        return true;
    }

    error =
        L"DEPENDENCY_PRECHECK_FAILED: Python 3.13+ and required dependencies are needed for local Transformers mode. "
        L"Install torch, transformers, sentence-transformers, and numpy. For script-to-video/script-to-episode also install diffusers, imageio, and imageio-ffmpeg.";
    return false;
}

bool GenerateLocalTransformersResult(const std::wstring& script, int taskIndex, const std::wstring& model, std::wstring& output, std::wstring& error)
{
    if (!RunPythonPreflightCheck(taskIndex, error))
    {
        return false;
    }

    std::wstring launcher = L"python";
    if (_wsystem(L"python -c \"import sys; raise SystemExit(0 if sys.version_info >= (3, 13) else 1)\" >nul 2>&1") != 0)
    {
        if (_wsystem(L"py -3.13 --version >nul 2>&1") == 0)
        {
            launcher = L"py -3.13";
        }
        else
        {
            error =
                L"DEPENDENCY_PRECHECK_FAILED: Python 3.13+ launcher not found. Install Python and ensure 'python' or 'py -3.13' is available.";
            return false;
        }
    }

    const std::wstring runnerPath = L"local_hf_runner.py";
    if (GetFileAttributesW(runnerPath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        error = L"local_hf_runner.py was not found in the app folder.";
        return false;
    }

    const std::wstring inputPath = CreateTempFilePath(L"pcI");
    const std::wstring outputPath = CreateTempFilePath(L"pcO");
    if (inputPath.empty() || outputPath.empty())
    {
        error = L"Failed to create temporary files for local Transformers run.";
        return false;
    }

    const std::wstring payload =
        L"{" 
        L"\"task\":\"" + EscapeJson(TaskNameFromIndex(taskIndex)) + L"\"," 
        L"\"model\":\"" + EscapeJson(model) + L"\"," 
        L"\"input\":\"" + EscapeJson(script) + L"\"}";

    if (!WriteUtf8File(inputPath, WideToUtf8(payload), error))
    {
        _wremove(inputPath.c_str());
        _wremove(outputPath.c_str());
        return false;
    }

    const std::wstring command =
        launcher + L" \"" + runnerPath + L"\" --input \"" + inputPath + L"\" --output \"" + outputPath + L"\"";

    const int exitCode = _wsystem(command.c_str());

    std::string resultJson;
    std::wstring readError;
    if (!ReadUtf8File(outputPath, resultJson, readError))
    {
        _wremove(inputPath.c_str());
        _wremove(outputPath.c_str());
        if (exitCode != 0)
        {
            error = L"Local Transformers process failed. Ensure Python and required packages are installed.";
        }
        else
        {
            error = readError;
        }
        return false;
    }

    _wremove(inputPath.c_str());
    _wremove(outputPath.c_str());

    const bool success =
        resultJson.find("\"success\":true") != std::string::npos ||
        resultJson.find("\"success\": true") != std::string::npos;

    if (!success)
    {
        const std::string remoteError = ExtractJsonStringField(resultJson, "error");
        error = remoteError.empty()
            ? L"Local Transformers run failed."
            : Utf8ToWide(remoteError);
        return false;
    }

    const std::string text = ExtractJsonStringField(resultJson, "output");
    if (text.empty())
    {
        error = L"Local Transformers output was empty or invalid.";
        return false;
    }

    output = Utf8ToWide(text);
    return !output.empty();
}

bool HttpPostJson(const std::wstring& url, const std::wstring& apiKey, const std::wstring& jsonBody, std::string& responseBody, DWORD& statusCode, std::wstring& error)
{
    URL_COMPONENTS components{};
    components.dwStructSize = sizeof(components);

    std::wstring hostName(256, L'\0');
    std::wstring urlPath(2048, L'\0');
    components.lpszHostName = hostName.data();
    components.dwHostNameLength = static_cast<DWORD>(hostName.size());
    components.lpszUrlPath = urlPath.data();
    components.dwUrlPathLength = static_cast<DWORD>(urlPath.size());
    components.dwSchemeLength = 1;

    if (!WinHttpCrackUrl(url.c_str(), static_cast<DWORD>(url.size()), 0, &components))
    {
        error = L"Invalid endpoint URL.";
        return false;
    }

    hostName.resize(components.dwHostNameLength);
    urlPath.resize(components.dwUrlPathLength);

    HINTERNET hSession = WinHttpOpen(L"ProjectC/1.0", WINHTTP_ACCESS_TYPE_DEFAULT_PROXY, WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);
    if (!hSession)
    {
        error = L"Failed to create HTTP session.";
        return false;
    }

    HINTERNET hConnect = WinHttpConnect(hSession, hostName.c_str(), components.nPort, 0);
    if (!hConnect)
    {
        WinHttpCloseHandle(hSession);
        error = L"Failed to connect to host.";
        return false;
    }

    const DWORD openFlags = (components.nScheme == INTERNET_SCHEME_HTTPS) ? WINHTTP_FLAG_SECURE : 0;
    HINTERNET hRequest = WinHttpOpenRequest(hConnect, L"POST", urlPath.c_str(), nullptr, WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, openFlags);
    if (!hRequest)
    {
        WinHttpCloseHandle(hConnect);
        WinHttpCloseHandle(hSession);
        error = L"Failed to create HTTP request.";
        return false;
    }

    DWORD timeoutMs = 30000;
    WinHttpSetTimeouts(hRequest, timeoutMs, timeoutMs, timeoutMs, timeoutMs);

    std::wstring headers = L"Content-Type: application/json\r\n";
    if (!apiKey.empty())
    {
        headers += L"Authorization: Bearer " + apiKey + L"\r\n";
    }

    const auto bodyUtf8 = WideToUtf8(jsonBody);
    const BOOL sendOk = WinHttpSendRequest(
        hRequest,
        headers.c_str(),
        static_cast<DWORD>(headers.size()),
        bodyUtf8.empty() ? WINHTTP_NO_REQUEST_DATA : const_cast<char*>(bodyUtf8.data()),
        static_cast<DWORD>(bodyUtf8.size()),
        static_cast<DWORD>(bodyUtf8.size()),
        0);

    if (!sendOk || !WinHttpReceiveResponse(hRequest, nullptr))
    {
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        WinHttpCloseHandle(hSession);
        error = L"HTTP request failed.";
        return false;
    }

    DWORD statusCodeSize = sizeof(statusCode);
    if (!WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER, WINHTTP_HEADER_NAME_BY_INDEX, &statusCode, &statusCodeSize, WINHTTP_NO_HEADER_INDEX))
    {
        statusCode = 0;
    }

    responseBody.clear();
    while (true)
    {
        DWORD available = 0;
        if (!WinHttpQueryDataAvailable(hRequest, &available))
        {
            break;
        }

        if (available == 0)
        {
            break;
        }

        std::vector<char> buffer(available);
        DWORD downloaded = 0;
        if (!WinHttpReadData(hRequest, buffer.data(), available, &downloaded))
        {
            break;
        }

        responseBody.append(buffer.data(), downloaded);

        if (responseBody.size() > 2 * 1024 * 1024)
        {
            error = L"Response too large.";
            WinHttpCloseHandle(hRequest);
            WinHttpCloseHandle(hConnect);
            WinHttpCloseHandle(hSession);
            return false;
        }
    }

    WinHttpCloseHandle(hRequest);
    WinHttpCloseHandle(hConnect);
    WinHttpCloseHandle(hSession);
    return true;
}

bool GenerateSceneOutlineFromScript(const std::wstring& script, std::wstring& outline, std::wstring& error)
{
    const std::wstring endpoint = L"http://localhost:11434/api/chat";
    std::wstring model = ReadEnvWide(L"OLLAMA_MODEL");
    if (model.empty())
    {
        model = L"llama3.2";
    }

    const std::wstring prompt =
        L"You are a scene planner. Return only a concise numbered scene outline for this script:\n\n" + script;

    const std::wstring requestBody =
        L"{" 
        L"\"model\":\"" + EscapeJson(model) + L"\"," 
        L"\"stream\":false,"
        L"\"messages\":[{\"role\":\"user\",\"content\":\"" + EscapeJson(prompt) + L"\"}]"
        L"}";

    std::string responseUtf8;
    DWORD statusCode = 0;
    if (!HttpPostJson(endpoint, L"", requestBody, responseUtf8, statusCode, error))
    {
        error = L"Failed to connect to local Ollama at http://localhost:11434. Start Ollama first.";
        return false;
    }

    if (statusCode == 404)
    {
        error = L"404 Not Found: local model is missing. Run: ollama pull " + model;
        return false;
    }

    if (statusCode < 200 || statusCode >= 300)
    {
        error = L"Local Ollama request failed with status " + std::to_wstring(statusCode) + L".";
        return false;
    }

    std::string content = ExtractJsonStringField(responseUtf8, "content");
    if (content.empty())
    {
        content = ExtractJsonStringField(responseUtf8, "response");
    }

    if (content.empty())
    {
        error = L"Could not parse local Ollama response content.";
        return false;
    }

    outline = Utf8ToWide(content);
    return !outline.empty();
}

bool GenerateHuggingFaceResult(const std::wstring& script, int taskIndex, const std::wstring& model, std::wstring& output, std::wstring& error)
{
    const std::wstring apiKey = ReadEnvWide(L"HUGGINGFACE_API_KEY");
    if (apiKey.empty())
    {
        error = L"HUGGINGFACE_API_KEY is not set. Add it to your environment or .env file.";
        return false;
    }

    std::wstring resolvedModel = model;
    if (resolvedModel.empty())
    {
        switch (taskIndex)
        {
        case 1:
            resolvedModel = L"facebook/bart-large-cnn";
            break;
        case 2:
            resolvedModel = L"distilbert/distilbert-base-uncased-finetuned-sst-2-english";
            break;
        case 3:
            resolvedModel = L"sentence-transformers/all-MiniLM-L6-v2";
            break;
        case 4:
            resolvedModel = L"dslim/bert-base-NER";
            break;
        case 5:
            resolvedModel = L"google/flan-t5-base";
            break;
        default:
            resolvedModel = L"google/flan-t5-base";
            break;
        }
    }

    std::wstring inputs = script;
    if (taskIndex == 0)
    {
        inputs = L"Create a concise numbered scene outline from this script:\n\n" + script;
    }
    else if (taskIndex == 5)
    {
        inputs =
            L"Write an original animated family-sitcom test episode script. "
            L"Do not reference existing copyrighted shows or characters. "
            L"Output sections: Title, Cold Open, Act 1, Act 2, Tag. "
            L"Include scene headers and dialogue. Prompt:\n\n" + script;
    }

    const std::wstring endpoint = L"https://api-inference.huggingface.co/models/" + resolvedModel;
    const std::wstring requestBody =
        L"{" 
        L"\"inputs\":\"" + EscapeJson(inputs) + L"\"," 
        L"\"options\":{\"wait_for_model\":true}}";

    std::string responseUtf8;
    DWORD statusCode = 0;
    if (!HttpPostJson(endpoint, apiKey, requestBody, responseUtf8, statusCode, error))
    {
        error = L"Failed to call Hugging Face Inference API.";
        return false;
    }

    if (statusCode < 200 || statusCode >= 300)
    {
        const std::wstring responseSnippet = Utf8ToWide(responseUtf8.substr(0, 220));
        error = L"Hugging Face request failed with status " + std::to_wstring(statusCode) + L". Response: " + responseSnippet;
        return false;
    }

    if (taskIndex == 3)
    {
        output = BuildEmbeddingPreview(responseUtf8);
        return !output.empty();
    }

    if (taskIndex == 4)
    {
        output = BuildEntityPreview(responseUtf8);
        return !output.empty();
    }

    const std::string text = ExtractFirstAvailableField(responseUtf8, { "generated_text", "summary_text", "translation_text", "label" });
    if (text.empty())
    {
        error = L"Could not parse Hugging Face response content.";
        return false;
    }

    output = Utf8ToWide(text);
    return !output.empty();
}

bool GenerateFromSelection(const std::wstring& script, int providerIndex, int taskIndex, const std::wstring& model, std::wstring& output, std::wstring& error)
{
    if (IsMediaGenerationTask(taskIndex) && providerIndex != 2)
    {
        error = L"Media generation (video/audio) currently runs only with Local Transformers (Python).";
        return false;
    }

    if (providerIndex == 0)
    {
        if (taskIndex != 0)
        {
            error = L"Local Ollama currently supports only Scene Outline task.";
            return false;
        }
        return GenerateSceneOutlineFromScript(script, output, error);
    }

    if (providerIndex == 1)
    {
        if (IsMediaGenerationTask(taskIndex))
        {
            error = L"Media generation is not supported through Hugging Face Inference API in this app. Use Local Transformers (Python).";
            return false;
        }
        return GenerateHuggingFaceResult(script, taskIndex, model, output, error);
    }

    return GenerateLocalTransformersResult(script, taskIndex, model, output, error);
}

int RunOllamaSmokeTest()
{
    std::wstring outline;
    std::wstring error;

    const std::vector<std::wstring> plots =
    {
        L"A washed-up magician discovers his tricks are real in a floating city where gravity fails each sunset.",
        L"A shy botanist finds a plant that grows memories and must stop a corporation from harvesting the town's past.",
        L"A teen radio host receives broadcasts from tomorrow and uses them to prevent disasters in her coastal village.",
        L"A retired monster hunter opens a bakery and gets pulled into one last case when pastries start predicting crimes.",
        L"Two rival astronauts crash on a moon made of mirrors and must cooperate before their reflections replace them."
    };

    const size_t plotIndex = static_cast<size_t>(GetTickCount64() % plots.size());
    const std::wstring script = plots[plotIndex];

    if (!GenerateSceneOutlineFromScript(script, outline, error))
    {
        std::wcerr << error << std::endl;
        std::ofstream resultFile("ollama-smoketest-result.txt", std::ios::trunc | std::ios::binary);
        resultFile << "PLOT: " << WideToUtf8(script) << "\n";
        resultFile << "ERROR: " << WideToUtf8(error) << "\n";
        return 2;
    }

    std::wcout << outline << std::endl;
    std::ofstream resultFile("ollama-smoketest-result.txt", std::ios::trunc | std::ios::binary);
    resultFile << "PLOT: " << WideToUtf8(script) << "\n";
    resultFile << WideToUtf8(outline) << "\n";
    return 0;
}

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                     _In_opt_ HINSTANCE hPrevInstance,
                     _In_ LPWSTR    lpCmdLine,
                     _In_ int       nCmdShow)
{
    UNREFERENCED_PARAMETER(hPrevInstance);

    LoadDotEnvFileIfPresent();

    if (lpCmdLine != nullptr)
    {
        const std::wstring cmdLine(lpCmdLine);
        if (cmdLine.find(L"--ollama-smoketest") != std::wstring::npos)
        {
            return RunOllamaSmokeTest();
        }
    }

    // TODO: Place code here.

    // Initialize global strings
    if (LoadStringW(hInstance, IDS_APP_TITLE, szTitle, MAX_LOADSTRING) == 0)
    {
        MessageBoxW(nullptr, L"Failed to load application title string.", L"Project C", MB_ICONERROR | MB_OK);
        return FALSE;
    }

    if (LoadStringW(hInstance, IDC_PROJECTC, szWindowClass, MAX_LOADSTRING) == 0)
    {
        MessageBoxW(nullptr, L"Failed to load window class string.", L"Project C", MB_ICONERROR | MB_OK);
        return FALSE;
    }

    if (MyRegisterClass(hInstance) == 0)
    {
        MessageBoxW(nullptr, L"Failed to register window class.", L"Project C", MB_ICONERROR | MB_OK);
        return FALSE;
    }

    // Perform application initialization:
    if (!InitInstance (hInstance, nCmdShow))
    {
        return FALSE;
    }

    HACCEL hAccelTable = LoadAccelerators(hInstance, MAKEINTRESOURCE(IDC_PROJECTC));

    MSG msg;

    // Main message loop:
    BOOL getMessageResult;
    while ((getMessageResult = GetMessage(&msg, nullptr, 0, 0)) > 0)
    {
        if (!TranslateAccelerator(msg.hwnd, hAccelTable, &msg))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

    if (getMessageResult == -1)
    {
        MessageBoxW(nullptr, L"A Windows message loop error occurred.", L"Project C", MB_ICONERROR | MB_OK);
        return -1;
    }

    return (int) msg.wParam;
}



//
//  FUNCTION: MyRegisterClass()
//
//  PURPOSE: Registers the window class.
//
ATOM MyRegisterClass(HINSTANCE hInstance)
{
    WNDCLASSEXW wcex;

    wcex.cbSize = sizeof(WNDCLASSEX);

    wcex.style          = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc    = WndProc;
    wcex.cbClsExtra     = 0;
    wcex.cbWndExtra     = 0;
    wcex.hInstance      = hInstance;
    wcex.hIcon          = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_PROJECTC));
    wcex.hCursor        = LoadCursor(nullptr, IDC_ARROW);
    wcex.hbrBackground  = (HBRUSH)(COLOR_WINDOW+1);
    wcex.lpszMenuName   = MAKEINTRESOURCEW(IDC_PROJECTC);
    wcex.lpszClassName  = szWindowClass;
    wcex.hIconSm        = LoadIcon(wcex.hInstance, MAKEINTRESOURCE(IDI_SMALL));

    return RegisterClassExW(&wcex);
}

//
//   FUNCTION: InitInstance(HINSTANCE, int)
//
//   PURPOSE: Saves instance handle and creates main window
//
//   COMMENTS:
//
//        In this function, we save the instance handle in a global variable and
//        create and display the main program window.
//
std::wstring GetControlText(HWND control)
{
    const int length = GetWindowTextLengthW(control);
    std::wstring text(static_cast<size_t>(length), L'\0');
    if (length > 0)
    {
        GetWindowTextW(control, text.data(), length + 1);
    }
    return text;
}

void SetStatusText(const std::wstring& text)
{
    if (gStatusLabel != nullptr)
    {
        SetWindowTextW(gStatusLabel, text.c_str());
    }
}

void ApplyDefaultModelForTask(int taskIndex)
{
    if (gModelInput == nullptr)
    {
        return;
    }

    std::wstring defaultModel;
    switch (taskIndex)
    {
    case 1:
        defaultModel = L"facebook/bart-large-cnn";
        break;
    case 2:
        defaultModel = L"distilbert/distilbert-base-uncased-finetuned-sst-2-english";
        break;
    case 3:
        defaultModel = L"sentence-transformers/all-MiniLM-L6-v2";
        break;
    case 4:
        defaultModel = L"dslim/bert-base-NER";
        break;
    case 5:
        defaultModel = L"google/flan-t5-base";
        break;
    case 6:
        defaultModel = L"cerspense/zeroscope_v2_576w";
        break;
    case 7:
        defaultModel = L"suno/bark-small";
        break;
    case 8:
        defaultModel = L"cerspense/zeroscope_v2_576w";
        break;
    default:
        defaultModel = L"google/flan-t5-base";
        break;
    }

    SetWindowTextW(gModelInput, defaultModel.c_str());
}

void CreateMainControls(HWND hWnd)
{
    HFONT font = static_cast<HFONT>(GetStockObject(DEFAULT_GUI_FONT));

    gScriptLabel = CreateWindowW(L"STATIC", L"Script / Input", WS_CHILD | WS_VISIBLE,
        0, 0, 0, 0, hWnd, nullptr, hInst, nullptr);
    gScriptInput = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"",
        WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_LEFT | ES_MULTILINE | ES_AUTOVSCROLL | WS_VSCROLL,
        0, 0, 0, 0, hWnd, reinterpret_cast<HMENU>(IDC_SCRIPT_INPUT), hInst, nullptr);

    gProviderLabel = CreateWindowW(L"STATIC", L"Provider", WS_CHILD | WS_VISIBLE,
        0, 0, 0, 0, hWnd, nullptr, hInst, nullptr);
    gProviderCombo = CreateWindowW(L"COMBOBOX", L"",
        WS_CHILD | WS_VISIBLE | WS_TABSTOP | CBS_DROPDOWNLIST,
        0, 0, 0, 0, hWnd, reinterpret_cast<HMENU>(IDC_PROVIDER_COMBO), hInst, nullptr);
    SendMessageW(gProviderCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Local Ollama"));
    SendMessageW(gProviderCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Hugging Face Inference API"));
    SendMessageW(gProviderCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Local Transformers (Python)"));
    SendMessageW(gProviderCombo, CB_SETCURSEL, 0, 0);

    gTaskLabel = CreateWindowW(L"STATIC", L"Task", WS_CHILD | WS_VISIBLE,
        0, 0, 0, 0, hWnd, nullptr, hInst, nullptr);
    gTaskCombo = CreateWindowW(L"COMBOBOX", L"",
        WS_CHILD | WS_VISIBLE | WS_TABSTOP | CBS_DROPDOWNLIST,
        0, 0, 0, 0, hWnd, reinterpret_cast<HMENU>(IDC_TASK_COMBO), hInst, nullptr);
    SendMessageW(gTaskCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Scene Outline (Transformers Generation)"));
    SendMessageW(gTaskCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Summarization (Transformers)"));
    SendMessageW(gTaskCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Sentiment Classification (Transformers)"));
    SendMessageW(gTaskCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Embeddings (Sentence-Transformers)"));
    SendMessageW(gTaskCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Named Entity Recognition (Token Classification)"));
    SendMessageW(gTaskCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Original Sitcom Script Generator"));
    SendMessageW(gTaskCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Script to Video (Local Diffusers)"));
    SendMessageW(gTaskCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Script to Audio (Local Transformers, WAV)"));
    SendMessageW(gTaskCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Script to Episode (Video + Audio + Mux)"));
    SendMessageW(gTaskCombo, CB_SETCURSEL, 0, 0);

    gModelLabel = CreateWindowW(L"STATIC", L"Model", WS_CHILD | WS_VISIBLE,
        0, 0, 0, 0, hWnd, nullptr, hInst, nullptr);
    gModelInput = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"",
        WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL,
        0, 0, 0, 0, hWnd, reinterpret_cast<HMENU>(IDC_MODEL_INPUT), hInst, nullptr);

    gGenerateButton = CreateWindowW(L"BUTTON", L"Generate",
        WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON,
        0, 0, 0, 0, hWnd, reinterpret_cast<HMENU>(IDC_GENERATE_BUTTON), hInst, nullptr);

    gOutputLabel = CreateWindowW(L"STATIC", L"Output", WS_CHILD | WS_VISIBLE,
        0, 0, 0, 0, hWnd, nullptr, hInst, nullptr);
    gOutputView = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"",
        WS_CHILD | WS_VISIBLE | ES_LEFT | ES_MULTILINE | ES_AUTOVSCROLL | ES_READONLY | WS_VSCROLL,
        0, 0, 0, 0, hWnd, reinterpret_cast<HMENU>(IDC_OUTPUT_VIEW), hInst, nullptr);

    gStatusLabel = CreateWindowW(L"STATIC", L"Ready.", WS_CHILD | WS_VISIBLE,
        0, 0, 0, 0, hWnd, reinterpret_cast<HMENU>(IDC_STATUS_LABEL), hInst, nullptr);

    for (HWND control : { gScriptLabel, gScriptInput, gProviderLabel, gProviderCombo, gTaskLabel, gTaskCombo, gModelLabel, gModelInput, gGenerateButton, gOutputLabel, gOutputView, gStatusLabel })
    {
        if (control != nullptr)
        {
            SendMessageW(control, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        }
    }

    SetWindowTextW(gScriptInput,
        L"A detective arrives at an abandoned theater, discovers coded notes backstage, "
        L"chases a masked suspect across the catwalk, and reveals the culprit to the police.");
    ApplyDefaultModelForTask(0);
}

void LayoutMainControls(HWND hWnd)
{
    RECT rc{};
    GetClientRect(hWnd, &rc);

    const int margin = 12;
    const int labelHeight = 18;
    const int rowGap = 6;
    const int editorHeight = (rc.bottom - rc.top) / 3;
    const int comboHeight = 260;
    const int buttonWidth = 120;
    const int statusHeight = 20;

    const int fullWidth = (rc.right - rc.left) - (margin * 2);
    const int columnWidth = (fullWidth - margin * 2 - buttonWidth) / 2;

    int y = margin;
    MoveWindow(gScriptLabel, margin, y, fullWidth, labelHeight, TRUE);
    MoveWindow(gScriptInput, margin, y + labelHeight + rowGap, fullWidth, editorHeight, TRUE);

    y += labelHeight + rowGap + editorHeight + margin;
    MoveWindow(gProviderLabel, margin, y, columnWidth, labelHeight, TRUE);
    MoveWindow(gTaskLabel, margin + columnWidth + margin, y, columnWidth, labelHeight, TRUE);
    MoveWindow(gProviderCombo, margin, y + labelHeight + rowGap, columnWidth, comboHeight, TRUE);
    MoveWindow(gTaskCombo, margin + columnWidth + margin, y + labelHeight + rowGap, columnWidth, comboHeight, TRUE);

    y += labelHeight + rowGap + 34 + margin;
    MoveWindow(gModelLabel, margin, y, fullWidth, labelHeight, TRUE);
    MoveWindow(gModelInput, margin, y + labelHeight + rowGap, fullWidth - buttonWidth - margin, 24, TRUE);
    MoveWindow(gGenerateButton, margin + fullWidth - buttonWidth, y + labelHeight + rowGap, buttonWidth, 24, TRUE);

    y += labelHeight + rowGap + 24 + margin;
    MoveWindow(gOutputLabel, margin, y, fullWidth, labelHeight, TRUE);
    const int outputHeight = (rc.bottom - y - statusHeight - margin * 2 - labelHeight - rowGap);
    MoveWindow(gOutputView, margin, y + labelHeight + rowGap, fullWidth, max(80, outputHeight), TRUE);

    MoveWindow(gStatusLabel, margin, rc.bottom - statusHeight - margin, fullWidth, statusHeight, TRUE);
}

void StartGenerationFromUi(HWND hWnd)
{
    if (gIsGenerating.exchange(true))
    {
        return;
    }

    const std::wstring script = GetControlText(gScriptInput);
    const std::wstring model = GetControlText(gModelInput);
    const int providerIndex = static_cast<int>(SendMessageW(gProviderCombo, CB_GETCURSEL, 0, 0));
    const int taskIndex = static_cast<int>(SendMessageW(gTaskCombo, CB_GETCURSEL, 0, 0));

    if (script.empty())
    {
        gIsGenerating = false;
        SetStatusText(L"Input is empty.");
        MessageBoxW(hWnd, L"Please enter script or text before running generation.", L"Missing Input", MB_ICONWARNING | MB_OK);
        return;
    }

    EnableWindow(gGenerateButton, FALSE);
    SetWindowTextW(gOutputView, L"Generating... please wait.");
    SetStatusText(BuildGenerationStatusText(taskIndex));

    std::thread([hWnd, script, model, providerIndex, taskIndex]()
    {
        auto* result = new GenerationResult();
        result->success = GenerateFromSelection(script, providerIndex, taskIndex, model, result->output, result->error);
        if (!result->success && providerIndex == 2 && IsLocalDependencyMissingError(result->error))
        {
            result->showSetupGuide = true;
        }
        PostMessageW(hWnd, WM_APP_GENERATION_COMPLETE, reinterpret_cast<WPARAM>(result), 0);
    }).detach();
}

BOOL InitInstance(HINSTANCE hInstance, int nCmdShow)
{
   hInst = hInstance; // Store instance handle in our global variable

   HWND hWnd = CreateWindowW(szWindowClass, szTitle, WS_OVERLAPPEDWINDOW,
      CW_USEDEFAULT, 0, 1120, 780, nullptr, nullptr, hInstance, nullptr);

   if (!hWnd)
   {
      return FALSE;
   }

   ShowWindow(hWnd, nCmdShow);
   UpdateWindow(hWnd);

   return TRUE;
}

//
//  FUNCTION: WndProc(HWND, UINT, WPARAM, LPARAM)
//
//  PURPOSE: Processes messages for the main window.
//
LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_CREATE:
        CreateMainControls(hWnd);
        LayoutMainControls(hWnd);
        return 0;
    case WM_SIZE:
        LayoutMainControls(hWnd);
        return 0;
    case WM_COMMAND:
        {
            int wmId = LOWORD(wParam);
            switch (wmId)
            {
            case IDM_ABOUT:
                DialogBox(hInst, MAKEINTRESOURCE(IDD_ABOUTBOX), hWnd, About);
                return 0;
            case IDM_SETUP_GUIDE:
                ShowLocalSetupGuideDialog(hWnd);
                return 0;
            case IDM_GENERATE_SCENE_OUTLINE:
            case IDC_GENERATE_BUTTON:
                StartGenerationFromUi(hWnd);
                return 0;
            case IDC_TASK_COMBO:
                if (HIWORD(wParam) == CBN_SELCHANGE)
                {
                    const int taskIndex = static_cast<int>(SendMessageW(gTaskCombo, CB_GETCURSEL, 0, 0));
                    ApplyDefaultModelForTask(taskIndex);

                    if (IsMediaGenerationTask(taskIndex))
                    {
                        const int providerIndex = static_cast<int>(SendMessageW(gProviderCombo, CB_GETCURSEL, 0, 0));
                        if (providerIndex != 2)
                        {
                            SendMessageW(gProviderCombo, CB_SETCURSEL, 2, 0);
                            SetStatusText(L"Provider switched to Local Transformers for media generation.");
                        }
                        else
                        {
                            SetStatusText(L"Media generation writes files to generated_videos/, generated_audio/, or generated_episodes/.");
                        }
                    }
                    else
                    {
                        SetStatusText(L"Ready.");
                    }
                }
                return 0;
            case IDC_PROVIDER_COMBO:
                if (HIWORD(wParam) == CBN_SELCHANGE)
                {
                    const int providerIndex = static_cast<int>(SendMessageW(gProviderCombo, CB_GETCURSEL, 0, 0));
                    const int taskIndex = static_cast<int>(SendMessageW(gTaskCombo, CB_GETCURSEL, 0, 0));
                    if (IsMediaGenerationTask(taskIndex) && providerIndex != 2)
                    {
                        SendMessageW(gProviderCombo, CB_SETCURSEL, 2, 0);
                        SetStatusText(L"Media generation requires Local Transformers. Provider reset automatically.");
                    }
                    else
                    {
                        SetStatusText(L"Ready.");
                    }
                }
                return 0;
            case IDM_EXIT:
                DestroyWindow(hWnd);
                return 0;
            default:
                break;
            }
        }
        break;
    case WM_APP_GENERATION_COMPLETE:
        {
            std::unique_ptr<GenerationResult> result(reinterpret_cast<GenerationResult*>(wParam));
            gIsGenerating = false;
            EnableWindow(gGenerateButton, TRUE);
            if (result != nullptr && result->success)
            {
                SetWindowTextW(gOutputView, result->output.c_str());
                SetStatusText(L"Completed successfully.");
            }
            else
            {
                const std::wstring messageText = (result != nullptr) ? result->error : L"Unknown generation failure.";
                SetWindowTextW(gOutputView, messageText.c_str());
                SetStatusText(L"Generation failed.");
                if (result != nullptr && result->showSetupGuide)
                {
                    ShowLocalSetupGuideDialog(hWnd);
                }
            }
            return 0;
        }
    case WM_PAINT:
        {
            PAINTSTRUCT ps;
            BeginPaint(hWnd, &ps);
            EndPaint(hWnd, &ps);
        }
        return 0;
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    default:
        break;
    }
    return DefWindowProc(hWnd, message, wParam, lParam);
}

// Message handler for about box.
INT_PTR CALLBACK About(HWND hDlg, UINT message, WPARAM wParam, LPARAM lParam)
{
    UNREFERENCED_PARAMETER(lParam);
    switch (message)
    {
    case WM_INITDIALOG:
        return (INT_PTR)TRUE;

    case WM_COMMAND:
        if (LOWORD(wParam) == IDOK || LOWORD(wParam) == IDCANCEL)
        {
            EndDialog(hDlg, LOWORD(wParam));
            return (INT_PTR)TRUE;
        }
        break;
    }
    return (INT_PTR)FALSE;
}
