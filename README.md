# api-stressor

A command-line tool for stress testing HTTP API endpoints. It sends repeated requests at a configurable rate using a JSON payload file, then reports success, failure, and response-time statistics.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

## Running the app

From the repository root:

```powershell
dotnet run --project src/Stressor.App -- `
  [--config <path-to-scenario.json>] `
  --url <endpoint-url> `
  --payload <path-to-payload.json> `
  --requests <count> `
  --interval <duration> `
  [--method <http-verb>] `
  [--auth <authorization-header-value>] `
  [--header <name-value>] `
  [--headers <path-to-headers.json>] `
  [--expect-status <code>] `
  [--report <path-to-report.json>] `
  [--load <gentle-pacing|fixed-rate|batch>] `
  [--batch <count>] `
  [--timeout <duration>] `
  [--cycles <count>] `
  [--cycle-interval <duration>] `
  [--verbose <failures|full>]
```

You can also build and run the executable directly:

```powershell
dotnet build src/Stressor.App
./src/Stressor.App/bin/Debug/net10.0/Stressor.App.exe --url ... --payload ... --requests ... --interval ...
```

## Getting help

Pass `--help` (or `-h`) to print usage information. This includes all command-line options, examples, supported HTTP methods, interval formats, authentication notes, and exit codes. No other arguments are required.

Pass `--version` to print the application version.

```powershell
dotnet run --project src/Stressor.App -- --help
dotnet run --project src/Stressor.App -- --version
```

When running the built executable:

```powershell
./src/Stressor.App/bin/Debug/net10.0/Stressor.App.exe --help
```

## Command-line options

| Option | Short | Required | Description |
|--------|-------|----------|-------------|
| `--config` | `-f` | No | Path to a JSON scenario config file (see below) |
| `--url` | `-u` | Yes* | Full URL of the API endpoint (must start with `http://` or `https://`) |
| `--payload` | `-p` | Yes* | Path to a JSON payload file (single body or multi-payload envelope) |
| `--requests` | `-r` | Yes* | Number of requests to send per cycle |
| `--interval` | `-i` | Yes* | Delay between consecutive request starts (see formats and load modes below) |
| `--method` | `-m` | No | HTTP method to use (default: `POST`) |
| `--auth` | `-a` | No | Authorization header value sent with each request (e.g. `Bearer <token>`) |
| `--header` | `-H` | No | Request header in `Name: Value` format (repeatable) |
| `--headers` | | No | Path to a JSON file of HTTP header name/value pairs |
| `--expect-status` | | No | HTTP status code that counts as success (repeatable; comma-separated allowed) |
| `--report` | | No | Path to write a JSON session report after completion |
| `--load` | `-l` | No | Load handling mode: `gentle-pacing` (default), `fixed-rate`, or `batch` |
| `--batch` | `-b` | No | Max parallel requests per wave (default: `1`; use with `--load batch`) |
| `--timeout` | `-t` | No | Per-request timeout (default: `100s`; same formats as `--interval`) |
| `--cycles` | `-c` | No | Number of cycles to run (default: `1`) |
| `--cycle-interval` | | No | Minimum wait after a cycle completes before the next cycle starts (default: `0s`; same formats as `--interval`) |
| `--verbose` | `-v` | No | Per-request output mode: `failures` (detail only on errors) or `full` (detail on every request) |
| `--help` | `-h` | No | Show usage information and exit |
| `--version` | | No | Print application version and exit |

\* `--url`, `--payload`, `--requests`, and `--interval` are required on the command line unless all are provided by `--config`.

### Scenario config file

Use `--config` to load settings from a JSON file instead of repeating long command lines. CLI flags override values from the config file only when you explicitly pass them.

Example `scenario.json`:

```json
{
  "url": "https://api.example.com/orders",
  "payload": "./payload.json",
  "method": "POST",
  "requests": 10,
  "interval": "1s",
  "cycles": 60,
  "auth": "Bearer your-token-here",
  "headers": {
    "X-Tenant-Id": "acme",
    "Accept": "application/json"
  },
  "expectStatus": [200, 201, 204],
  "report": "./results/session-report.json",
  "verbose": "failures",
  "load": "gentle-pacing",
  "batch": 1,
  "timeout": "100s",
  "cycleInterval": "0s"
}
```

Run with:

```powershell
dotnet run --project src/Stressor.App -- --config ./scenario.json
```

Override a single value from the command line:

```powershell
dotnet run --project src/Stressor.App -- --config ./scenario.json --cycles 10
```

Property names use camelCase. Duration fields use the same formats as `--interval`. Paths in `payload` are resolved relative to the config file's directory.

### Supported HTTP methods

`GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS`

For `POST`, `PUT`, and `PATCH`, the JSON payload file is sent as the request body. For other methods, the payload file is still required and validated, but no body is attached to the request.

### Authentication

Use `--auth` when the API requires an `Authorization` header. Pass the full header value; the tool does not add a scheme for you.

```powershell
--auth "Bearer your-token-here"
```

If `--auth` is omitted, no authorization header is sent.

### Custom HTTP headers

Send additional headers with `--header` (repeatable), `--headers` (JSON file), or a `headers` object in the scenario config.

```powershell
--header "X-Api-Key: abc123" `
--headers ./extra-headers.json
```

Headers file example (`extra-headers.json`):

```json
{
  "X-Correlation-Id": "run-42"
}
```

Merge precedence (lowest to highest): scenario `headers` → `--headers` file → each `--header` → `--auth` (which sets `Authorization` last).

For body-bearing methods, a `Content-Type` header overrides the default `application/json` body type.

### Expected status codes

By default, any **2xx** response counts as success. Use `--expect-status` (repeatable) or `expectStatus` in the scenario config to define a custom set of success codes.

```powershell
--expect-status 200 `
--expect-status 201,204
```

Explicit CLI flags replace the config list when provided. Status codes must be integers from 100 to 599. This is useful for strict contract tests (only `200`) or negative-path checks where a specific `4xx` is the expected outcome.

### JSON report export

Write a machine-readable session report with `--report` or `report` in the scenario config:

```powershell
--report ./results/session-report.json
```

The report includes session metadata, redacted configuration (no auth or header values), summary counts, latency percentiles, and per-request outcomes. It is written after every run, including partial or cancelled sessions. Use it in CI to assert on `exitCode`, failure counts, or latency thresholds.

### Interval formats

The `--interval` value can be written in several ways:

- Seconds: `1s`, `2.5s`
- Milliseconds: `500ms`, `250ms`
- Standard time span: `00:00:01`, `00:00:00.500`

The `--timeout` and `--cycle-interval` values use the same formats (`--timeout` default: `100s`; `--cycle-interval` default: `0s`).

## How load is applied

Use `--load` to choose how requests are scheduled. Each **cycle** sends `--requests` calls. Use `--interval` for spacing within a cycle. Use `--cycle-interval` for an optional rest between cycles (default `0s`, which keeps pacing continuous across cycle boundaries). The total number of requests in a session is:

```
requests × cycles
```

### `gentle-pacing` (default)

The first request in a session starts immediately. Each subsequent request waits until `--interval` has elapsed since the previous request **started**. If a request takes longer than the interval, the next request starts as soon as the slow one finishes. Only one request is in flight at a time.

For example, `--requests 10 --interval 1s --cycles 1` sends 10 requests with about one second between each start (when responses are fast).

So `--requests 10 --interval 1s --cycles 60` sends 600 requests with about one second between consecutive starts (roughly 10 minutes of pacing when responses are fast).

Use `--cycle-interval` to pause between cycles instead of running back-to-back. For example, `--requests 10 --interval 1s --cycles 5 --cycle-interval 30s` sends five bursts of 10 requests (about 10 seconds each), with a 30-second rest between bursts.

### `fixed-rate`

Each request starts every `--interval` on a fixed session timeline (`0s`, `interval`, `2 × interval`, …), even if earlier requests are still running. Multiple requests may be in flight at once.

For example, `--load fixed-rate --requests 10 --interval 1s --cycles 1` starts one request per second for 10 seconds regardless of response time.

With `--verbose failures` or `--verbose full`, per-request output includes a session-wide `(index/total)` prefix on the header line.

### `batch`

Sends up to `--batch` requests in parallel per **wave** within each cycle. Use `--load batch` when `--batch` is greater than 1. `--interval` is the minimum delay between **wave starts** (not individual request starts). A value of `0s` is allowed in batch mode for back-to-back waves.

`--batch` must not exceed `--requests`. If there are more requests in a cycle than `--batch`, the remainder is sent in a final partial wave (for example, `--requests 12 --batch 5` sends waves of 5, 5, and 2).

For example, `--load batch --requests 100 --batch 20 --interval 500ms --cycles 1` sends 100 requests in five waves of 20, with 500ms between wave starts.

## Payload file

The payload file must contain valid JSON. There are two formats:

### Single body (default)

Any JSON value — object, array, or primitive — is sent unchanged on every request. Example `payload.json`:

```json
{
  "orderId": 12345,
  "quantity": 2
}
```

Root-level arrays are also sent as a single body:

```json
[1, 2, 3]
```

### Multi-payload envelope

To rotate through multiple request bodies within each cycle, use a root object with **only** a `payloads` array. Example `payloads.json`:

```json
{
  "payloads": [
    {"orderId": 1, "quantity": 1},
    {"orderId": 2, "quantity": 3},
    {"orderId": 3, "quantity": 5}
  ]
}
```

Each array element is sent as the request body for one request, in order. If there are more requests in a cycle than payloads, the tool wraps back to the first item. Each new cycle starts again from the first payload.

Objects that include a `payloads` field alongside other fields (for example `{"orderId": 1, "payloads": [1, 2]}`) are **not** treated as an envelope — the full file is sent as a single body.

Pass the file path with `--payload`:

```powershell
--payload ./payload.json
```

For body-bearing methods (`POST`, `PUT`, `PATCH`), the selected payload is sent as the request body. For other methods, the payload file is still required and validated, but no body is attached.

## Example

```powershell
dotnet run --project src/Stressor.App -- `
  --url https://api.example.com/orders `
  --payload ./payload.json `
  --method POST `
  --requests 10 `
  --interval 1s `
  --cycles 60
```

This sends 600 POST requests to the orders endpoint at a rate of 10 per second for about one minute.

### Fixed-rate load

```powershell
dotnet run --project src/Stressor.App -- `
  --url https://api.example.com/orders `
  --payload ./payload.json `
  --method POST `
  --requests 10 `
  --interval 1s `
  --cycles 60 `
  --load fixed-rate `
  --verbose failures
```

This starts one request per second for 600 seconds regardless of how long responses take. Use `--verbose failures` to print detail only when a request fails or is cancelled.

### Batch load

```powershell
dotnet run --project src/Stressor.App -- `
  --url https://api.example.com/orders `
  --payload ./payload.json `
  --method POST `
  --requests 100 `
  --batch 20 `
  --interval 500ms `
  --cycles 3 `
  --load batch `
  --verbose failures
```

This sends 300 requests in waves of 20, with 500ms between wave starts.

### Smoke test with full verbose

```powershell
dotnet run --project src/Stressor.App -- `
  --url https://api.example.com/orders `
  --payload ./payload.json `
  --method POST `
  --requests 1 `
  --interval 1s `
  --cycles 1 `
  --verbose full
```

Use `--verbose full` for short runs where you want request body, response body, and HTTP status on every request.

### Cycle rest between bursts

```powershell
dotnet run --project src/Stressor.App -- `
  --url https://api.example.com/orders `
  --payload ./payload.json `
  --method POST `
  --requests 10 `
  --interval 1s `
  --cycles 5 `
  --cycle-interval 30s
```

This sends five bursts of 10 requests with a 30-second pause between each burst.

### Authenticated endpoint

```powershell
dotnet run --project src/Stressor.App -- `
  --url https://api.example.com/orders `
  --payload ./payload.json `
  --auth "Bearer your-token-here" `
  --method POST `
  --requests 10 `
  --interval 1s `
  --cycles 60
```

## Output

While running, the tool prints a summary at the start, a line per cycle, and a final session report:

```
Stress test starting
  URL:      https://api.example.com/orders
  Method:   POST
  Auth:     configured
  Rate:     10 requests/cycle, 1s between starts
  Load:     gentle-pacing
  Timeout:  100s
  Cycle gap: 30s
  Cycles:   5 (50 total requests)

Cycle 1/5  OK 10  Fail 0  Avg 45ms
...
Session complete
  Succeeded: 598
  Failed:    2
  Latency:   min 32ms  avg 47ms  max 210ms  p50 45ms  p95 180ms  p99 205ms
```

- **OK** — requests that matched the expected HTTP status (default: any 2xx)
- **Fail** — requests that returned an error status or could not complete
- **Avg** — average response time for successful requests in that cycle
- **Auth: configured** — shown when `--auth` was provided (the token itself is not printed)

### Verbose output

Use `--verbose failures` for stress runs: per-request detail is printed only when a request fails or is cancelled. Use `--verbose full` for short smoke/debug runs where every request is printed.

Each verbose block includes session index, cycle/request position, payload variant (`payload N/M`), request and response bodies (truncated at 1024 characters), and an outcome line with HTTP status.

At session end, when verbose is active and any requests failed or were cancelled, a compact failure digest is appended:

```
Failures (2):
  (12/600) HTTP 503 payload 3/8 120ms
  (47/600) timeout payload 1/8 80ms
```

## Stopping early

Press **Ctrl+C** to stop the session. The tool stops scheduling new requests or waves, waits for already-started work to finish, prints a partial report, and exits. With `fixed-rate` or `batch`, multiple requests may still be in flight when you cancel.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | All requests completed successfully |
| `1` | One or more requests failed, or the command-line arguments were invalid |
| `2` | The session was cancelled (for example, via Ctrl+C) |
