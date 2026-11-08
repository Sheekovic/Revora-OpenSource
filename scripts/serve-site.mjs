import {createServer} from 'node:http';
import {readFile, realpath, stat} from 'node:fs/promises';
import {extname, resolve, sep} from 'node:path';
import {fileURLToPath, pathToFileURL} from 'node:url';

const defaultRoot = fileURLToPath(new URL('../website/site/', import.meta.url));
const types = {'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8','.png':'image/png','.ico':'image/x-icon','.svg':'image/svg+xml','.txt':'text/plain; charset=utf-8'};

export async function createSiteServer({root = defaultRoot} = {}) {
  const siteRoot = await realpath(root);
  return createServer(async (request, response) => {
    response.setHeader('X-Content-Type-Options', 'nosniff');
    response.setHeader('Referrer-Policy', 'strict-origin-when-cross-origin');
    response.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'none'; object-src 'none'; base-uri 'self'; form-action 'none'; frame-ancestors 'none'");
    if (!['GET','HEAD'].includes(request.method)) { response.writeHead(405, {'Allow':'GET, HEAD'}).end(); return; }
    try {
      const requested = decodeURIComponent(new URL(request.url, 'http://127.0.0.1').pathname);
      if (requested === '/__health') { response.writeHead(200, {'Content-Type':'application/json','Cache-Control':'no-store'}).end(request.method === 'HEAD' ? undefined : JSON.stringify({state:'ready',component:'Revora.Static'})); return; }
      if (requested.includes('\0') || requested.includes('\\') || requested.split('/').some(part => part.startsWith('.'))) { response.writeHead(403).end('Forbidden'); return; }
      const candidate = resolve(siteRoot, '.' + (requested.endsWith('/') ? requested + 'index.html' : requested));
      if (!candidate.startsWith(siteRoot + sep)) { response.writeHead(403).end('Forbidden'); return; }
      const canonical = await realpath(candidate);
      if (!canonical.startsWith(siteRoot + sep)) { response.writeHead(403).end('Forbidden'); return; }
      if (!(await stat(canonical)).isFile()) { response.writeHead(404).end('Not found'); return; }
      const data = await readFile(canonical);
      response.writeHead(200, {'Content-Type': types[extname(canonical)] || 'application/octet-stream', 'Content-Length': data.length, 'Cache-Control': extname(canonical) === '.png' ? 'public, max-age=86400' : 'no-cache'});
      response.end(request.method === 'HEAD' ? undefined : data);
    } catch (error) { response.writeHead(error instanceof URIError ? 400 : 404).end('Not found'); }
  });
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  const portIndex = process.argv.indexOf('--port');
  const port = portIndex === -1 ? 5080 : Number(process.argv[portIndex + 1]);
  if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Use a port between 1024 and 65535.');
  const server = await createSiteServer();
  server.on('error', () => { console.error('The static website could not start. Check whether its loopback port is already in use.'); process.exitCode = 1; });
  server.listen(port, '127.0.0.1', () => console.log(`Revora static website: http://127.0.0.1:${port}`));
  for (const signal of ['SIGTERM','SIGINT']) process.on(signal, () => server.close(() => process.exit(0)));
}
