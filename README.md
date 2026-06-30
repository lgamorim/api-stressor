# api-stressor

A command-line tool for stress testing HTTP API endpoints. It sends repeated requests at a configurable rate using a JSON payload file, then reports success, failure, and response-time statistics.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

## Running the app

From the repository root:

```powershell
dotnet run --project src/Stressor.App -- `
  --url <endpoint-url> `
  --payload <path-to-payload.json> `
  --requests <count> `
  --interval <duration> `
  --cycles <count> `
  [--method <http-verb>] `
  [--auth <authorization-header-value>]
```

You can also build and run the executable directly:

```powershell
dotnet build src/Stressor.App
./src/Stressor.App/bin/Debug/net10.0/Stressor.App.exe --url ... --payload ... --requests ... --interval ... --cycles ...
```

## Getting help

Pass `--help` (or `-h`) to print usage information. This includes all command-line options, examples, supported HTTP methods, interval formats, authentication notes, and exit codes. No other arguments are required.

```powershell
dotnet run --project src/Stressor.App -- --help
```

When running the built executable:

```powershell
./src/Stressor.App/bin/Debug/net10.0/Stressor.App.exe --help
```

## Command-line options

| Option | Short | Required | Description |
|--------|-------|----------|-------------|
| `--url` | `-u` | Yes | Full URL of the API endpoint (must start with `http://` or `https://`) |
| `--payload` | `-p` | Yes | Path to a JSON payload file (single body or multi-payload envelope) |
| `--requests` | `-r` | Yes | Number of requests to send during each interval |
| `--interval` | `-i` | Yes | Length of each interval (see formats below) |
| `--cycles` | `-c` | Yes | How many intervals to run |
| `--method` | `-m` | No | HTTP method to use (default: `POST`) |
| `--auth` | `-a` | No | Authorization header value sent with each request (e.g. `Bearer <token>`) |
| `--help` | `-h` | No | Show usage information and exit |

### Supported HTTP methods

`GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS`

For `POST`, `PUT`, and `PATCH`, the JSON payload file is sent as the request body. For other methods, the payload file is still required and validated, but no body is attached to the request.

### Authentication

Use `--auth` when the API requires an `Authorization` header. Pass the full header value; the tool does not add a scheme for you.

```powershell
--auth "Bearer your-token-here"
```

If `--auth` is omitted, no authorization header is sent.

### Interval formats

The `--interval` value can be written in several ways:

- Seconds: `1s`, `2.5s`
- Milliseconds: `500ms`, `250ms`
- Standard time span: `00:00:01`, `00:00:00.500`

## How load is applied

Each **cycle** is one interval. Within a cycle, the tool sends `--requests` calls spread evenly across `--interval`. For example, `--requests 10 --interval 1s` sends about one request every 100 milliseconds for one second.

The total number of requests in a session is:

```
requests × cycles
```

So `--requests 10 --interval 1s --cycles 60` sends 600 requests over roughly 60 seconds.

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
  Rate:     10 requests / 1s
  Cycles:   60 (600 total requests)

Cycle 1/60  OK 10  Fail 0  Avg 45ms
...
Session complete
  Succeeded: 598
  Failed:    2
  Latency:   min 32ms  avg 47ms  max 210ms
```

- **OK** — requests that returned a successful HTTP status (2xx)
- **Fail** — requests that returned an error status or could not complete
- **Avg** — average response time for successful requests in that cycle
- **Auth: configured** — shown when `--auth` was provided (the token itself is not printed)

## Stopping early

Press **Ctrl+C** to stop the session. The tool finishes the current in-flight request, prints a partial report, and exits.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | All requests completed successfully |
| `1` | One or more requests failed, or the command-line arguments were invalid |
| `2` | The session was cancelled (for example, via Ctrl+C) |
