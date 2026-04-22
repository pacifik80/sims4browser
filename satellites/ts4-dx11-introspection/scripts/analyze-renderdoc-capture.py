#!/usr/bin/env python
import argparse
import hashlib
import json
import os
import signal
import subprocess
import sys
import tempfile
import textwrap
import time
from collections import Counter, defaultdict
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_QRENDERDOC = Path(r"C:\Program Files\RenderDoc\qrenderdoc.exe")
DEFAULT_RENDERDOCCMD = Path(r"C:\Program Files\RenderDoc\renderdoccmd.exe")
DEFAULT_CATALOG = REPO_ROOT / "satellites" / "ts4-dx11-introspection" / "docs" / "raw" / "shader-catalog.json"


def parse_args():
    parser = argparse.ArgumentParser(description="Analyze a RenderDoc capture and extract pass/draw/shader summaries.")
    parser.add_argument("--capture", required=True, help="Path to the .rdc capture file.")
    parser.add_argument("--output-dir", help="Directory for extracted analysis artifacts. Defaults to <capture-dir>/analysis/<capture-stem>.")
    parser.add_argument("--catalog", default=str(DEFAULT_CATALOG), help="Path to shader-catalog.json for hash matching.")
    parser.add_argument("--qrenderdoc", default=str(DEFAULT_QRENDERDOC), help="Path to qrenderdoc.exe.")
    parser.add_argument("--renderdoccmd", default=str(DEFAULT_RENDERDOCCMD), help="Path to renderdoccmd.exe.")
    parser.add_argument("--timeout-seconds", type=int, default=180, help="How long to wait for qrenderdoc extraction.")
    return parser.parse_args()


def stable_path(path_str: str) -> Path:
    return Path(path_str).expanduser().resolve()


def make_output_dir(capture_path: Path, explicit: str | None) -> Path:
    if explicit:
        output_dir = stable_path(explicit)
    else:
        output_dir = capture_path.parent / "analysis" / capture_path.stem
    output_dir.mkdir(parents=True, exist_ok=True)
    return output_dir


def load_catalog(path: Path) -> dict[tuple[str, str], dict]:
    if not path.exists():
        return {}

    with path.open("r", encoding="utf-8") as handle:
        payload = json.load(handle)

    entries = payload.get("Entries", [])
    lookup: dict[tuple[str, str], dict] = {}
    for entry in entries:
        key = (entry.get("Hash", "").lower(), entry.get("Stage", ""))
        lookup[key] = entry
    return lookup


def run_thumbnail(renderdoccmd: Path, capture_path: Path, output_dir: Path) -> Path | None:
    if not renderdoccmd.exists():
        return None

    thumb_path = output_dir / f"{capture_path.stem}.thumb.png"
    command = [
        str(renderdoccmd),
        "thumb",
        f"--out={thumb_path}",
        str(capture_path),
    ]

    completed = subprocess.run(command, capture_output=True, text=True, timeout=120)
    if completed.returncode != 0:
        return None
    return thumb_path if thumb_path.exists() else None


def build_ui_script(output_json_path: Path) -> str:
    output_json = str(output_json_path).replace("\\", "\\\\")
    return textwrap.dedent(
        f"""
        import hashlib
        import json
        import time
        import traceback
        import renderdoc

        ctx = pyrenderdoc
        OUTPUT_JSON = r"{output_json}"

        def safe_name(action):
            try:
                if action.customName:
                    return action.customName
            except Exception:
                pass

            try:
                return action.GetName(action.eventId)
            except Exception:
                try:
                    return action.GetName(action.actionId)
                except Exception:
                    return "<unnamed>"

        def scalarize(value):
            if value is None:
                return None

            if isinstance(value, (str, int, float, bool)):
                return value

            if isinstance(value, bytes):
                return value.hex()

            if isinstance(value, tuple):
                return [scalarize(v) for v in value]

            return str(value)

        def format_to_dict(fmt):
            if fmt is None:
                return None

            result = {{}}
            for key in ["type", "compType", "compCount", "byteWidth", "srgbCorrected"]:
                try:
                    result[key] = scalarize(getattr(fmt, key))
                except Exception:
                    pass
            return result

        def descriptor_to_dict(descriptor):
            if descriptor is None:
                return None

            result = {{}}
            for key in [
                "resource",
                "secondary",
                "view",
                "type",
                "flags",
                "textureType",
                "firstMip",
                "numMips",
                "firstSlice",
                "numSlices",
                "byteOffset",
                "byteSize",
                "elementByteSize",
                "bufferStructCount",
            ]:
                try:
                    result[key] = scalarize(getattr(descriptor, key))
                except Exception:
                    pass

            try:
                result["format"] = format_to_dict(descriptor.format)
            except Exception:
                pass

            return result

        def sampler_to_dict(sampler):
            if sampler is None:
                return None

            result = {{}}
            for key in [
                "type",
                "filter",
                "addressU",
                "addressV",
                "addressW",
                "compareFunction",
                "maxAnisotropy",
                "minLOD",
                "maxLOD",
                "mipBias",
            ]:
                try:
                    result[key] = scalarize(getattr(sampler, key))
                except Exception:
                    pass
            return result

        def used_descriptor_to_dict(index, used):
            result = {{
                "slot": index,
                "access": scalarize(getattr(used, "access", None)),
            }}

            try:
                result["descriptor"] = descriptor_to_dict(used.descriptor)
            except Exception:
                result["descriptor"] = None

            try:
                result["sampler"] = sampler_to_dict(used.sampler)
            except Exception:
                result["sampler"] = None

            return result

        def shader_constant_type_to_dict(shader_type):
            if shader_type is None:
                return None

            result = {{}}
            for key in ["name", "baseType", "rows", "columns", "elements", "flags", "arrayByteStride", "matrixByteStride"]:
                try:
                    result[key] = scalarize(getattr(shader_type, key))
                except Exception:
                    pass
            return result

        def shader_constant_to_dict(variable):
            result = {{
                "name": scalarize(getattr(variable, "name", None)),
                "byteOffset": scalarize(getattr(variable, "byteOffset", None)),
                "bitFieldOffset": scalarize(getattr(variable, "bitFieldOffset", None)),
                "bitFieldSize": scalarize(getattr(variable, "bitFieldSize", None)),
                "defaultValue": scalarize(getattr(variable, "defaultValue", None)),
            }}

            try:
                result["type"] = shader_constant_type_to_dict(variable.type)
            except Exception:
                result["type"] = None

            return result

        def constant_block_to_dict(block):
            result = {{
                "name": scalarize(getattr(block, "name", None)),
                "byteSize": scalarize(getattr(block, "byteSize", None)),
                "bufferBacked": scalarize(getattr(block, "bufferBacked", None)),
                "compileConstants": scalarize(getattr(block, "compileConstants", None)),
                "bindArraySize": scalarize(getattr(block, "bindArraySize", None)),
                "fixedBindSetOrSpace": scalarize(getattr(block, "fixedBindSetOrSpace", None)),
                "fixedBindNumber": scalarize(getattr(block, "fixedBindNumber", None)),
                "variables": [],
            }}

            try:
                for variable in block.variables:
                    result["variables"].append(shader_constant_to_dict(variable))
            except Exception:
                pass

            return result

        def shader_resource_to_dict(resource):
            return {{
                "name": scalarize(getattr(resource, "name", None)),
                "descriptorType": scalarize(getattr(resource, "descriptorType", None)),
                "textureType": scalarize(getattr(resource, "textureType", None)),
                "isReadOnly": scalarize(getattr(resource, "isReadOnly", None)),
                "isTexture": scalarize(getattr(resource, "isTexture", None)),
                "hasSampler": scalarize(getattr(resource, "hasSampler", None)),
                "isInputAttachment": scalarize(getattr(resource, "isInputAttachment", None)),
                "bindArraySize": scalarize(getattr(resource, "bindArraySize", None)),
                "fixedBindSetOrSpace": scalarize(getattr(resource, "fixedBindSetOrSpace", None)),
                "fixedBindNumber": scalarize(getattr(resource, "fixedBindNumber", None)),
                "variableType": shader_constant_type_to_dict(getattr(resource, "variableType", None)),
            }}

        def sig_param_to_dict(param):
            result = {{}}
            for key in ["varName", "semanticName", "semanticIdxName", "semanticIndex", "systemValue", "compType", "regIndex", "channelUsedMask"]:
                try:
                    result[key] = scalarize(getattr(param, key))
                except Exception:
                    pass

            try:
                result["format"] = format_to_dict(param.varType)
            except Exception:
                pass

            return result

        def reflection_to_dict(reflection):
            if reflection is None:
                return None

            raw_bytes = bytes(reflection.rawBytes)
            return {{
                "resourceId": scalarize(getattr(reflection, "resourceId", None)),
                "stage": scalarize(getattr(reflection, "stage", None)),
                "entryPoint": scalarize(getattr(reflection, "entryPoint", None)),
                "encoding": scalarize(getattr(reflection, "encoding", None)),
                "rawByteLength": len(raw_bytes),
                "sha256": hashlib.sha256(raw_bytes).hexdigest() if raw_bytes else None,
                "readOnlyResources": [shader_resource_to_dict(resource) for resource in reflection.readOnlyResources],
                "samplers": [
                    {{
                        "name": scalarize(getattr(sampler, "name", None)),
                        "fixedBindSetOrSpace": scalarize(getattr(sampler, "fixedBindSetOrSpace", None)),
                        "fixedBindNumber": scalarize(getattr(sampler, "fixedBindNumber", None)),
                        "bindArraySize": scalarize(getattr(sampler, "bindArraySize", None)),
                    }}
                    for sampler in reflection.samplers
                ],
                "constantBlocks": [constant_block_to_dict(block) for block in reflection.constantBlocks],
                "inputSignature": [sig_param_to_dict(param) for param in reflection.inputSignature],
                "outputSignature": [sig_param_to_dict(param) for param in reflection.outputSignature],
            }}

        def output_target_to_dict(target):
            try:
                return descriptor_to_dict(target)
            except Exception:
                return scalarize(target)

        def get_draw_summary(action, marker_stack):
            ctx.SetEventID([], action.eventId, action.eventId)
            pipe = ctx.CurPipelineState()

            stage_map = {{}}
            for stage in [renderdoc.ShaderStage.Vertex, renderdoc.ShaderStage.Pixel]:
                reflection = pipe.GetShaderReflection(stage)
                stage_map[str(stage)] = {{
                    "entryPoint": scalarize(pipe.GetShaderEntryPoint(stage)),
                    "reflection": reflection_to_dict(reflection),
                    "boundReadOnlyResources": [used_descriptor_to_dict(i, used) for i, used in enumerate(pipe.GetReadOnlyResources(stage))],
                    "boundConstantBlocks": [used_descriptor_to_dict(i, used) for i, used in enumerate(pipe.GetConstantBlocks(stage))],
                    "boundSamplers": [sampler_to_dict(sampler) for sampler in pipe.GetSamplers(stage)],
                }}

            return {{
                "actionId": int(action.actionId),
                "eventId": int(action.eventId),
                "name": safe_name(action),
                "flags": str(action.flags),
                "markerPath": list(marker_stack),
                "topMarker": marker_stack[0] if marker_stack else None,
                "numIndices": int(action.numIndices),
                "numInstances": int(action.numInstances),
                "baseVertex": int(action.baseVertex),
                "vertexOffset": int(action.vertexOffset),
                "indexOffset": int(action.indexOffset),
                "instanceOffset": int(action.instanceOffset),
                "topology": scalarize(pipe.GetPrimitiveTopology()),
                "vertexInputs": [
                    {{
                        "name": scalarize(attribute.name),
                        "vertexBuffer": scalarize(attribute.vertexBuffer),
                        "byteOffset": scalarize(attribute.byteOffset),
                        "perInstance": scalarize(attribute.perInstance),
                        "instanceRate": scalarize(attribute.instanceRate),
                        "used": scalarize(attribute.used),
                        "format": format_to_dict(attribute.format),
                    }}
                    for attribute in pipe.GetVertexInputs()
                ],
                "outputTargets": [output_target_to_dict(target) for target in pipe.GetOutputTargets()],
                "depthTarget": output_target_to_dict(pipe.GetDepthTarget()),
                "colorBlends": [
                    {{
                        "enabled": scalarize(getattr(blend, "enabled", None)),
                        "logicOperationEnabled": scalarize(getattr(blend, "logicOperationEnabled", None)),
                        "writeMask": scalarize(getattr(blend, "writeMask", None)),
                        "source": scalarize(getattr(blend, "source", None)),
                        "destination": scalarize(getattr(blend, "destination", None)),
                        "operation": scalarize(getattr(blend, "operation", None)),
                    }}
                    for blend in pipe.GetColorBlends()
                ],
                "shaders": stage_map,
            }}

        def walk(actions, marker_stack, out_actions, out_draws):
            for action in actions:
                name = safe_name(action)
                flags = str(action.flags)
                action_record = {{
                    "actionId": int(action.actionId),
                    "eventId": int(action.eventId),
                    "name": name,
                    "flags": flags,
                    "depth": len(marker_stack),
                    "markerPath": list(marker_stack),
                    "numIndices": int(action.numIndices),
                    "numInstances": int(action.numInstances),
                    "children": int(len(action.children)),
                }}
                out_actions.append(action_record)

                next_stack = marker_stack
                if "PushMarker" in flags:
                    next_stack = marker_stack + [name]

                if "Drawcall" in flags:
                    out_draws.append(get_draw_summary(action, marker_stack))

                if len(action.children) > 0:
                    walk(action.children, next_stack, out_actions, out_draws)

        result = {{}}

        try:
            if not ctx.IsCaptureLoaded():
                raise RuntimeError("Capture is not loaded in qrenderdoc context")

            actions = []
            draws = []
            walk(ctx.CurRootActions(), [], actions, draws)

            try:
                frame_info = ctx.FrameInfo()
                frame_dict = {{}}
                for key in ["frameNumber", "fileOffset", "captureTime", "stats"]:
                    try:
                        frame_dict[key] = scalarize(getattr(frame_info, key))
                    except Exception:
                        pass
            except Exception:
                frame_dict = None

            result["captureFilename"] = ctx.GetCaptureFilename()
            result["currentEvent"] = ctx.CurEvent()
            result["frameInfo"] = frame_dict
            result["actions"] = actions
            result["draws"] = draws
        except Exception as exc:
            result["error"] = repr(exc)
            result["traceback"] = traceback.format_exc()

        with open(OUTPUT_JSON, "w", encoding="utf-8") as handle:
            json.dump(result, handle, indent=2)
            handle.flush()

        time.sleep(120)
        """
    )


def terminate_process(process: subprocess.Popen) -> None:
    if process.poll() is not None:
        return

    try:
        process.terminate()
        process.wait(timeout=10)
        return
    except Exception:
        pass

    try:
        process.kill()
        process.wait(timeout=10)
    except Exception:
        pass


def run_qrenderdoc_extract(qrenderdoc: Path, capture_path: Path, output_dir: Path, timeout_seconds: int) -> dict:
    qrenderdoc_json = output_dir / "renderdoc-extract.raw.json"
    ui_script_path = output_dir / "_qrenderdoc_extract.py"
    ui_script_path.write_text(build_ui_script(qrenderdoc_json), encoding="utf-8")

    process = subprocess.Popen(
        [
            str(qrenderdoc),
            "--ui-py",
            str(ui_script_path),
            str(capture_path),
        ],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )

    deadline = time.time() + timeout_seconds
    try:
        while time.time() < deadline:
            if qrenderdoc_json.exists():
                try:
                    with qrenderdoc_json.open("r", encoding="utf-8") as handle:
                        payload = json.load(handle)
                    return payload
                except json.JSONDecodeError:
                    pass
            time.sleep(0.5)
    finally:
        terminate_process(process)
        try:
            ui_script_path.unlink(missing_ok=True)
        except Exception:
            pass

    raise TimeoutError(f"Timed out waiting for qrenderdoc analysis of {capture_path}")


def summarize_descriptor_ids(descriptor_entries: list[dict]) -> list[str]:
    ids = []
    for entry in descriptor_entries or []:
        descriptor = entry.get("descriptor") or {}
        resource = descriptor.get("resource")
        view = descriptor.get("view")
        if resource and resource != "ResourceId::0":
            ids.append(f"{entry.get('slot')}:{resource}")
        elif view and view != "ResourceId::0":
            ids.append(f"{entry.get('slot')}:{view}")
    return ids


def enrich_draws(raw_payload: dict, catalog_lookup: dict[tuple[str, str], dict]) -> list[dict]:
    draws = []
    for draw in raw_payload.get("draws", []):
        shaders = draw.get("shaders", {})
        vs = shaders.get("ShaderStage.Vertex", {})
        ps = shaders.get("ShaderStage.Pixel", {})

        vs_reflection = (vs.get("reflection") or {})
        ps_reflection = (ps.get("reflection") or {})
        vs_hash = vs_reflection.get("sha256")
        ps_hash = ps_reflection.get("sha256")

        draw_summary = {
            "actionId": draw.get("actionId"),
            "eventId": draw.get("eventId"),
            "name": draw.get("name"),
            "flags": draw.get("flags"),
            "markerPath": draw.get("markerPath") or [],
            "topMarker": draw.get("topMarker"),
            "numIndices": draw.get("numIndices"),
            "numInstances": draw.get("numInstances"),
            "topology": draw.get("topology"),
            "vertexInputNames": [attribute.get("name") for attribute in draw.get("vertexInputs", []) if attribute.get("name")],
            "outputResources": summarize_descriptor_ids(draw.get("outputTargets", [])),
            "depthResource": summarize_descriptor_ids([draw.get("depthTarget", {})]),
            "vs": {
                "hash": vs_hash,
                "entryPoint": vs.get("entryPoint"),
                "resourceNames": [resource.get("name") for resource in vs_reflection.get("readOnlyResources", []) if resource.get("name")],
                "constantBlocks": [block.get("name") for block in vs_reflection.get("constantBlocks", []) if block.get("name")],
                "inputSemantics": [param.get("semanticName") for param in vs_reflection.get("inputSignature", []) if param.get("semanticName")],
                "boundResourceIds": summarize_descriptor_ids(vs.get("boundReadOnlyResources", [])),
                "boundConstantBufferIds": summarize_descriptor_ids(vs.get("boundConstantBlocks", [])),
                "catalogMatch": catalog_lookup.get((str(vs_hash).lower(), "vs")),
            },
            "ps": {
                "hash": ps_hash,
                "entryPoint": ps.get("entryPoint"),
                "resourceNames": [resource.get("name") for resource in ps_reflection.get("readOnlyResources", []) if resource.get("name")],
                "constantBlocks": [block.get("name") for block in ps_reflection.get("constantBlocks", []) if block.get("name")],
                "outputSemantics": [param.get("semanticName") for param in ps_reflection.get("outputSignature", []) if param.get("semanticName")],
                "boundResourceIds": summarize_descriptor_ids(ps.get("boundReadOnlyResources", [])),
                "boundConstantBufferIds": summarize_descriptor_ids(ps.get("boundConstantBlocks", [])),
                "catalogMatch": catalog_lookup.get((str(ps_hash).lower(), "ps")),
            },
        }

        draws.append(draw_summary)

    return draws


def build_summary(raw_payload: dict, draws: list[dict], thumb_path: Path | None) -> dict:
    pass_counter = Counter()
    per_pass_draws: dict[str, list[dict]] = defaultdict(list)
    vs_hashes = Counter()
    ps_hashes = Counter()
    catalog_matched_vs = Counter()
    catalog_matched_ps = Counter()

    for draw in draws:
        top_marker = draw.get("topMarker") or "<unmarked>"
        pass_counter[top_marker] += 1
        per_pass_draws[top_marker].append(draw)

        vs_hash = draw["vs"].get("hash")
        ps_hash = draw["ps"].get("hash")
        if vs_hash:
            vs_hashes[vs_hash] += 1
        if ps_hash:
            ps_hashes[ps_hash] += 1
        if draw["vs"].get("catalogMatch"):
            catalog_matched_vs[vs_hash] += 1
        if draw["ps"].get("catalogMatch"):
            catalog_matched_ps[ps_hash] += 1

    pass_summaries = []
    for pass_name, pass_draw_list in pass_counter.most_common():
        sample_draw = per_pass_draws[pass_name][0]
        unique_vs = sorted({draw["vs"].get("hash") for draw in per_pass_draws[pass_name] if draw["vs"].get("hash")})
        unique_ps = sorted({draw["ps"].get("hash") for draw in per_pass_draws[pass_name] if draw["ps"].get("hash")})
        pass_summaries.append(
            {
                "pass": pass_name,
                "drawCount": pass_draw_list,
                "sampleEventId": sample_draw.get("eventId"),
                "sampleVsHash": sample_draw["vs"].get("hash"),
                "samplePsHash": sample_draw["ps"].get("hash"),
                "uniqueVsCount": len(unique_vs),
                "uniquePsCount": len(unique_ps),
                "sampleResourceNames": sorted(set(sample_draw["ps"].get("resourceNames", []) + sample_draw["vs"].get("resourceNames", []))),
            }
        )

    summary = {
        "captureFilename": raw_payload.get("captureFilename"),
        "thumbnailPath": str(thumb_path) if thumb_path else None,
        "actionCount": len(raw_payload.get("actions", [])),
        "drawCount": len(draws),
        "presentCount": sum(1 for action in raw_payload.get("actions", []) if "Present" in str(action.get("flags"))),
        "topPasses": pass_summaries,
        "uniqueVsHashes": len(vs_hashes),
        "uniquePsHashes": len(ps_hashes),
        "catalogMatchedVs": len(catalog_matched_vs),
        "catalogMatchedPs": len(catalog_matched_ps),
        "topVsHashes": [{"hash": shader_hash, "draws": count} for shader_hash, count in vs_hashes.most_common(10)],
        "topPsHashes": [{"hash": shader_hash, "draws": count} for shader_hash, count in ps_hashes.most_common(10)],
    }

    return summary


def write_json(path: Path, payload: dict | list) -> None:
    with path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2)


def draw_md_table(rows: list[list[str]]) -> str:
    if not rows:
        return "_none_"

    widths = [max(len(str(row[index])) for row in rows) for index in range(len(rows[0]))]
    lines = []
    for row_index, row in enumerate(rows):
        padded = [str(cell).ljust(widths[index]) for index, cell in enumerate(row)]
        lines.append("| " + " | ".join(padded) + " |")
        if row_index == 0:
            lines.append("| " + " | ".join("-" * width for width in widths) + " |")
    return "\n".join(lines)


def write_markdown(path: Path, capture_path: Path, summary: dict, draws: list[dict]) -> None:
    top_pass_rows = [["Pass", "Draws", "Sample EID", "Sample VS", "Sample PS", "Resources"]]
    for entry in summary["topPasses"][:12]:
        top_pass_rows.append(
            [
                entry["pass"],
                str(entry["drawCount"]),
                str(entry["sampleEventId"]),
                (entry["sampleVsHash"] or "-")[:12],
                (entry["samplePsHash"] or "-")[:12],
                ", ".join(entry["sampleResourceNames"][:6]) or "-",
            ]
        )

    interesting_draws = sorted(
        draws,
        key=lambda draw: (
            0 if draw["ps"].get("catalogMatch") else 1,
            -(draw.get("numIndices") or 0),
            draw.get("eventId") or 0,
        ),
    )[:20]

    draw_rows = [["EID", "Pass", "Indices", "VS", "PS", "PS resources", "CBs"]]
    for draw in interesting_draws:
        draw_rows.append(
            [
                str(draw["eventId"]),
                draw.get("topMarker") or "<unmarked>",
                str(draw.get("numIndices") or 0),
                (draw["vs"].get("hash") or "-")[:12],
                (draw["ps"].get("hash") or "-")[:12],
                ", ".join(draw["ps"].get("resourceNames", [])[:5]) or "-",
                ", ".join(draw["ps"].get("constantBlocks", [])[:5]) or "-",
            ]
        )

    lines = [
        f"# RenderDoc Capture Summary: {capture_path.name}",
        "",
        f"- Capture: `{capture_path}`",
        f"- Draw calls: `{summary['drawCount']}`",
        f"- Actions: `{summary['actionCount']}`",
        f"- Present events: `{summary['presentCount']}`",
        f"- Unique VS hashes: `{summary['uniqueVsHashes']}` (`catalog matched: {summary['catalogMatchedVs']}`)",
        f"- Unique PS hashes: `{summary['uniquePsHashes']}` (`catalog matched: {summary['catalogMatchedPs']}`)",
    ]

    if summary.get("thumbnailPath"):
        lines.extend(
            [
                f"- Thumbnail: `{summary['thumbnailPath']}`",
                "",
                f"![Capture thumbnail]({summary['thumbnailPath']})",
            ]
        )

    lines.extend(
        [
            "",
            "## Pass Summary",
            "",
            draw_md_table(top_pass_rows),
            "",
            "## Representative Draws",
            "",
            draw_md_table(draw_rows),
            "",
            "## Notes",
            "",
            "- `Sample VS/PS` are SHA-256 hashes of the replayed shader bytecode from RenderDoc reflection.",
            "- `catalog matched` means the hash was found in `docs/raw/shader-catalog.json`.",
            "- This report only covers the `.rdc` files currently present; if multiple frames were intended, each RenderDoc capture should normally exist as a separate `.rdc` file.",
        ]
    )

    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    capture_path = stable_path(args.capture)
    if not capture_path.exists():
        raise FileNotFoundError(f"Capture not found: {capture_path}")

    qrenderdoc = stable_path(args.qrenderdoc)
    if not qrenderdoc.exists():
        raise FileNotFoundError(f"qrenderdoc.exe not found: {qrenderdoc}")

    renderdoccmd = stable_path(args.renderdoccmd)
    output_dir = make_output_dir(capture_path, args.output_dir)
    catalog_lookup = load_catalog(stable_path(args.catalog))

    thumb_path = run_thumbnail(renderdoccmd, capture_path, output_dir)
    raw_payload = run_qrenderdoc_extract(qrenderdoc, capture_path, output_dir, args.timeout_seconds)
    draws = enrich_draws(raw_payload, catalog_lookup)
    summary = build_summary(raw_payload, draws, thumb_path)

    write_json(output_dir / "renderdoc-extract.raw.json", raw_payload)
    write_json(output_dir / "renderdoc-draws.enriched.json", draws)
    write_json(output_dir / "renderdoc-summary.json", summary)
    write_markdown(output_dir / "renderdoc-summary.md", capture_path, summary, draws)

    print(f"Analysis written to {output_dir}")
    print(f"Summary: {output_dir / 'renderdoc-summary.md'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
