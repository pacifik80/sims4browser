# TS4 DX11 Introspection Event Schema

The MVP uses UTF-8 JSON Lines over a named pipe and persists the same objects into `.jsonl` files.

## Envelope

All events share these top-level fields:

- `schema_version`: currently `1`
- `event_type`: stable event discriminator
- `timestamp_utc`: ISO-8601 UTC timestamp from the native runtime
- `pid`: game process id
- `session_id`: runtime session correlation id

## `frame_boundary`

- `frame_index`
- `present_flags`
- `sync_interval`
- `capture_active`
- `detail_mode`: currently `continuous`
- `bound_vs_hash`
- `bound_ps_hash`
- `command_lists_recorded_in_frame`
- `command_lists_executed_in_frame`
- `draw_count_in_frame`

## `shader_created`

- `shader_hash`: SHA-256 over the raw DXBC/DXIL bytecode buffer
- `stage`: `vs`, `ps`, `gs`, `hs`, or `ds`
- `bytecode_size`
- `shader_pointer`
- `reflection`

### Reflection payload

- `instruction_count`
- `temp_register_count`
- `bound_resources`
- `constant_buffers`
- `input_parameters`
- `output_parameters`

## `bookmark`

- `frame_index`
- `bound_vs_hash`
- `bound_ps_hash`

`F10` is optional and only emits a bookmark event. It does not gate logging.

## `state_definition`

- `frame_index`
- `state_kind`: `blend`, `depth_stencil`, or `rasterizer`
- `state_id`: session-local stable id derived from the COM pointer
- `summary`

## `draw_call`

- `frame_index`
- `draw_index`
- `draw_kind`
- `vertex_count`
- `index_count`
- `instance_count`
- `start_vertex_location`
- `start_index_location`
- `bound_vs_hash`
- `bound_ps_hash`
- `vs_srv_slots`
- `ps_srv_slots`
- `vs_cb_slots`
- `ps_cb_slots`
- `ps_sampler_slots`
- `blend_state_id`
- `depth_stencil_state_id`
- `rasterizer_state_id`

## `command_list_recorded`

- `frame_index`
- `command_list`
- `restore_state`
- `hresult`

This event is emitted when a deferred context finalizes a recorded command list through `FinishCommandList`.

## `command_list_submitted`

- `frame_index`
- `command_list`
- `restore_state`

This event is emitted when an immediate context submits a command list through `ExecuteCommandList`.

## Logging mode

The current runtime strategy is:

- always-on `frame_boundary`
- always-on `shader_created`
- always-on command-list telemetry through `command_list_recorded` and `command_list_submitted`
- always-on `draw_call` with current pipeline summaries attached when the runtime safely exposes draw calls
- deduplicated `state_definition`
- optional user bookmarks on `F10`

This keeps the log analytically useful without requiring the user to predict "interesting" moments.
