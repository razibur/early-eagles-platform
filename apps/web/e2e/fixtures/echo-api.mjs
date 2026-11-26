// A stand-in for the API in the browser tests (P0-071.5): answers every
// request with what it received, so a test can check exactly what the web
// app's /api/* forwarding sent on. Started by playwright.config.ts.
import { createServer } from "node:http"

const port = Number(process.argv[2] ?? 3299)

createServer((req, res) => {
  if (req.url === "/__ready") {
    res.writeHead(200).end("ok")
    return
  }
  const chunks = []
  req.on("data", (chunk) => chunks.push(chunk))
  req.on("end", () => {
    res.writeHead(200, { "content-type": "application/json" })
    res.end(JSON.stringify({ method: req.method, path: req.url, headers: req.headers, body: Buffer.concat(chunks).toString("utf8") }))
  })
}).listen(port, "127.0.0.1")
