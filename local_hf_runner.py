import argparse
import json
import os
import re
import subprocess
import traceback
from typing import Any, Optional


def default_model_for_task(task: str) -> str:
    if task == "summarization":
        return "facebook/bart-large-cnn"
    if task == "sentiment":
        return "distilbert/distilbert-base-uncased-finetuned-sst-2-english"
    if task == "embeddings":
        return "sentence-transformers/all-MiniLM-L6-v2"
    if task == "ner":
        return "dslim/bert-base-NER"
    if task == "sitcom_script":
        return "google/flan-t5-base"
    if task in ("script_to_video", "script_to_episode"):
        return "cerspense/zeroscope_v2_576w"
    if task in ("text_to_audio", "script_to_audio"):
        return "suno/bark-small"
    return "google/flan-t5-base"


def format_entities(items) -> str:
    lines = ["Named entities:"]
    for item in items[:12]:
        word = item.get("word", "")
        label = item.get("entity_group") or item.get("entity") or "entity"
        score = item.get("score")
        if score is None:
            lines.append(f"- {word} ({label})")
        else:
            lines.append(f"- {word} ({label}, {score:.2f})")
    if len(lines) == 1:
        return "No entities found."
    return "\n".join(lines)


def _extract_scene_prompts(script_text: str, max_scenes: int = 8):
    candidates = []
    for raw_line in script_text.splitlines():
        line = raw_line.strip()
        if not line:
            continue
        if line.startswith(("INT.", "EXT.")):
            candidates.append(line)
            continue
        if re.match(r"^(SCENE|Scene)\s+\d+", line):
            candidates.append(line)
            continue
        if len(line) > 28 and line.endswith((".", "!", "?")):
            candidates.append(line)

    if not candidates:
        candidates = [
            "Animated suburban neighborhood at sunrise with cinematic lighting.",
            "Family living room comedic tension with vibrant colors.",
            "Town square chase scene with exaggerated expressions.",
            "Warm sunset resolution scene in front of family house.",
        ]

    return candidates[:max_scenes]


def _build_video_pipeline(model: str, config: dict[str, Any]):
    import torch
    from diffusers import DiffusionPipeline

    precision = str(config.get("precision", "fp16")).lower()
    torch_dtype = torch.float16 if precision == "fp16" else torch.float32
    use_cuda = torch.cuda.is_available()

    pipeline_kwargs = {}
    if use_cuda:
        pipeline_kwargs["torch_dtype"] = torch_dtype

    pipe = DiffusionPipeline.from_pretrained(model, **pipeline_kwargs)

    offload_mode = str(config.get("offload_mode", "sequential")).lower()
    if use_cuda and offload_mode != "none":
        if offload_mode == "model" and hasattr(pipe, "enable_model_cpu_offload"):
            pipe.enable_model_cpu_offload()
        elif hasattr(pipe, "enable_sequential_cpu_offload"):
            pipe.enable_sequential_cpu_offload()
        elif hasattr(pipe, "enable_model_cpu_offload"):
            pipe.enable_model_cpu_offload()
        else:
            pipe = pipe.to("cuda")
    elif use_cuda:
        pipe = pipe.to("cuda")
    elif hasattr(pipe, "to"):
        pipe = pipe.to("cpu")

    return pipe


def _generate_video_from_script(model: str, script_text: str, video_config: Optional[dict[str, Any]] = None) -> tuple[str, dict[str, Any]]:
    import imageio.v2 as imageio
    import numpy as np
    import torch

    config = {
        "offload_mode": "sequential",
        "precision": "fp16",
        "max_scenes": 4,
        "num_frames": 16,
        "height": 320,
        "width": 576,
        "num_inference_steps": 20,
        "guidance_scale": 7.0,
        "fps": 8,
        "seed": None,
    }
    if isinstance(video_config, dict):
        config.update(video_config)

    scene_prompts = _extract_scene_prompts(script_text, max_scenes=int(config["max_scenes"]))
    pipe = _build_video_pipeline(model, config)

    all_frames = []
    for prompt in scene_prompts:
        full_prompt = (
            "cinematic tv episode shot, coherent characters, high detail, smooth motion: " + prompt
        )

        generator = None
        if config.get("seed") is not None:
            seed = int(config["seed"])
            if torch.cuda.is_available():
                generator = torch.Generator(device="cuda").manual_seed(seed)
            else:
                generator = torch.Generator().manual_seed(seed)

        result = pipe(
            prompt=full_prompt,
            num_inference_steps=int(config["num_inference_steps"]),
            guidance_scale=float(config["guidance_scale"]),
            num_frames=int(config["num_frames"]),
            height=int(config["height"]),
            width=int(config["width"]),
            generator=generator,
        )

        clip_frames = []
        if hasattr(result, "frames") and result.frames:
            clip_frames = result.frames[0] if isinstance(result.frames[0], list) else result.frames
        elif hasattr(result, "images") and result.images:
            clip_frames = result.images

        if not clip_frames:
            raise RuntimeError("Video pipeline did not return frames.")

        all_frames.extend(clip_frames)

    output_dir = os.path.join(os.getcwd(), "generated_videos")
    os.makedirs(output_dir, exist_ok=True)
    output_path = os.path.join(output_dir, "script_video.mp4")

    with imageio.get_writer(output_path, fps=int(config["fps"])) as writer:
        for frame in all_frames:
            writer.append_data(np.asarray(frame))

    return output_path, config


def _save_audio_wav(audio_values, sample_rate: int, output_path: str) -> None:
    import wave

    import numpy as np

    audio = np.asarray(audio_values)
    if audio.ndim > 1:
        audio = audio[0]

    if audio.dtype.kind == "f":
        max_abs = float(np.max(np.abs(audio))) if audio.size else 1.0
        if max_abs > 1.0:
            audio = audio / max_abs
        audio = np.clip(audio, -1.0, 1.0)
        pcm16 = (audio * 32767.0).astype(np.int16)
    elif audio.dtype != np.int16:
        pcm16 = audio.astype(np.int16)
    else:
        pcm16 = audio

    with wave.open(output_path, "wb") as wav_file:
        wav_file.setnchannels(1)
        wav_file.setsampwidth(2)
        wav_file.setframerate(int(sample_rate))
        wav_file.writeframes(pcm16.tobytes())


def _generate_audio_from_text(model: str, input_text: str, audio_config: Optional[dict[str, Any]] = None) -> tuple[str, dict[str, Any]]:
    from transformers import pipeline

    config = {
        "max_new_tokens": 320,
        "filename": "generated_audio.wav",
    }
    if isinstance(audio_config, dict):
        config.update(audio_config)

    synth = pipeline("text-to-audio", model=model)
    result = synth(input_text, forward_params={"max_new_tokens": int(config["max_new_tokens"])})

    audio_values = result.get("audio")
    sample_rate = int(result.get("sampling_rate", 24000))
    if audio_values is None:
        raise RuntimeError("Audio pipeline did not return audio data.")

    output_dir = os.path.join(os.getcwd(), "generated_audio")
    os.makedirs(output_dir, exist_ok=True)
    output_path = os.path.join(output_dir, str(config["filename"]))

    _save_audio_wav(audio_values, sample_rate, output_path)
    return output_path, config


def _mux_video_and_audio(video_path: str, audio_path: str, episode_config: Optional[dict[str, Any]] = None) -> tuple[str, dict[str, Any]]:
    import imageio_ffmpeg

    config = {
        "filename": "script_episode.mp4",
    }
    if isinstance(episode_config, dict):
        config.update(episode_config)

    output_dir = os.path.join(os.getcwd(), "generated_episodes")
    os.makedirs(output_dir, exist_ok=True)
    output_path = os.path.join(output_dir, str(config["filename"]))

    ffmpeg_exe = imageio_ffmpeg.get_ffmpeg_exe()
    mux_command = [
        ffmpeg_exe,
        "-y",
        "-i",
        video_path,
        "-i",
        audio_path,
        "-c:v",
        "copy",
        "-c:a",
        "aac",
        "-shortest",
        output_path,
    ]

    process = subprocess.run(mux_command, capture_output=True, text=True)
    if process.returncode != 0:
        fallback_command = [
            ffmpeg_exe,
            "-y",
            "-i",
            video_path,
            "-i",
            audio_path,
            "-c:v",
            "libx264",
            "-pix_fmt",
            "yuv420p",
            "-c:a",
            "aac",
            "-shortest",
            output_path,
        ]
        process = subprocess.run(fallback_command, capture_output=True, text=True)
        if process.returncode != 0:
            stderr_tail = (process.stderr or "").strip()[-400:]
            raise RuntimeError(f"Failed to merge video and audio with ffmpeg. {stderr_tail}")

    return output_path, config


def _generate_episode_from_script(video_model: str, audio_model: str, script_text: str, episode_options: Optional[dict[str, Any]] = None) -> tuple[str, dict[str, Any]]:
    options = episode_options if isinstance(episode_options, dict) else {}
    video_options = options.get("video") if isinstance(options.get("video"), dict) else options
    audio_options = options.get("audio") if isinstance(options.get("audio"), dict) else {}

    video_path, used_video_config = _generate_video_from_script(video_model, script_text, video_options)
    audio_path, used_audio_config = _generate_audio_from_text(audio_model, script_text, audio_options)
    episode_path, used_episode_config = _mux_video_and_audio(video_path, audio_path, options.get("episode"))

    details = {
        "video": used_video_config,
        "audio": used_audio_config,
        "episode": used_episode_config,
    }
    return episode_path, details


def run_task(task: str, model: str, text: str, options: Optional[dict[str, Any]] = None) -> str:
    model = model or default_model_for_task(task)

    if task == "embeddings":
        from sentence_transformers import SentenceTransformer

        embedder = SentenceTransformer(model)
        vector = embedder.encode(text)
        preview = ", ".join(f"{x:.4f}" for x in vector[:16])
        return f"Embedding dimension: {len(vector)}\nPreview: [{preview}]"

    from transformers import pipeline

    if task == "scene_outline":
        generator = pipeline("text2text-generation", model=model)
        prompt = (
            "Create a concise numbered scene outline from this script. "
            "Return only the outline.\n\n" + text
        )
        result = generator(prompt, max_new_tokens=220, do_sample=False)
        return result[0].get("generated_text", "")

    if task == "summarization":
        summarizer = pipeline("summarization", model=model)
        result = summarizer(text, max_length=160, min_length=32, do_sample=False)
        return result[0].get("summary_text", "")

    if task == "sentiment":
        classifier = pipeline("text-classification", model=model)
        result = classifier(text)
        item = result[0] if isinstance(result, list) and result else {}
        label = item.get("label", "unknown")
        score = item.get("score", 0.0)
        return f"Sentiment: {label} ({score:.3f})"

    if task == "ner":
        ner_pipeline = pipeline("token-classification", model=model, aggregation_strategy="simple")
        result = ner_pipeline(text)
        return format_entities(result)

    if task == "sitcom_script":
        generator = pipeline("text2text-generation", model=model)
        prompt = (
            "Write an original animated family-sitcom test episode script. "
            "Do not reference existing copyrighted shows or characters. "
            "Output sections: Title, Cold Open, Act 1, Act 2, Tag. "
            "Include scene headers and dialogue.\n\n" + text
        )
        result = generator(prompt, max_new_tokens=420, do_sample=False)
        return result[0].get("generated_text", "")

    if task == "script_to_video":
        video_path, used_config = _generate_video_from_script(model, text, options)
        return (
            f"Video generated: {video_path}\n"
            f"Offload mode: {used_config['offload_mode']}\n"
            f"Precision: {used_config['precision']}\n"
            f"Frames per scene: {used_config['num_frames']}"
        )

    if task in ("text_to_audio", "script_to_audio"):
        audio_path, used_config = _generate_audio_from_text(model, text, options)
        return (
            f"Audio generated: {audio_path}\n"
            f"Model: {model}\n"
            f"Max new tokens: {used_config['max_new_tokens']}"
        )

    if task == "script_to_episode":
        video_model = str(options.get("video_model") or model or default_model_for_task("script_to_video"))
        audio_model = str(options.get("audio_model") or default_model_for_task("script_to_audio"))
        episode_path, details = _generate_episode_from_script(video_model, audio_model, text, options)
        return (
            f"Episode generated: {episode_path}\n"
            f"Video model: {video_model}\n"
            f"Audio model: {audio_model}\n"
            f"Frames per scene: {details['video']['num_frames']}\n"
            f"Audio max new tokens: {details['audio']['max_new_tokens']}"
        )

    raise ValueError(f"Unsupported task: {task}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    response = {"success": False, "output": "", "error": ""}

    try:
        with open(args.input, "r", encoding="utf-8") as f:
            payload = json.load(f)

        task = str(payload.get("task", "scene_outline"))
        model = str(payload.get("model", ""))
        text = str(payload.get("input", ""))
        options = payload.get("options", {})
        if options is None:
            options = {}
        if not isinstance(options, dict):
            raise ValueError("'options' must be a JSON object when provided.")

        if not text.strip():
            raise ValueError("Input text is empty.")

        output = run_task(task, model, text, options)
        response["success"] = True
        response["output"] = output
    except Exception as ex:
        response["success"] = False
        response["error"] = f"{type(ex).__name__}: {ex}"
        response["trace"] = traceback.format_exc(limit=2)

    with open(args.output, "w", encoding="utf-8") as f:
        json.dump(response, f, ensure_ascii=False)

    return 0 if response["success"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
