import { cp, mkdir, access } from "node:fs/promises";
import path from "node:path";

const dist = path.resolve("dist");
const assets = path.join(dist, "assets");
const routedAssets = path.join(dist, "admin", "assets");

await access(assets);
await mkdir(path.dirname(routedAssets), { recursive: true });
await cp(assets, routedAssets, { recursive: true, force: true });

console.log("Prepared /admin/assets files for Cloudflare subpath routing.");
