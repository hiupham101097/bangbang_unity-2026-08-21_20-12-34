# Authoritative WebSocket deployment

The existing Cloudflare Worker is a separate REST API. It cannot be used as the root URL of `BangLiveGateway`.

## Render Blueprint

1. In Render choose **New > Blueprint** and connect this GitHub repository.
2. Render detects the root `render.yaml` and deploys `Server`.
3. Verify `https://<service>.onrender.com/health` returns HTTP 200.
4. Set Unity `GameBootstrap.liveWebSocketUrl` to `wss://<service>.onrender.com`.
5. Keep `cloudflareWorkerUrl` only for `BangNetworkClient` REST calls.

The Blueprint generates `BANG_AUTH_SECRET`. A free instance may sleep and `/tmp` storage is ephemeral; use persistent storage before public release.
