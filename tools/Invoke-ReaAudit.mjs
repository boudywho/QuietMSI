import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { basename, join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

// Static analysis only: target assemblies are never loaded or executed.
// Usage: node tools/Invoke-ReaAudit.mjs REA_PACKAGE_ROOT OUTPUT_DIR ASSEMBLY...
const [packageRoot, outputDirectory, ...targets] = process.argv.slice(2);
if (!packageRoot || !outputDirectory || !targets.length) {
  throw new Error('Pass REA package directory, output directory, and explicit assembly paths.');
}
const output = resolve(outputDirectory);
await mkdir(output, { recursive: true });
const require = createRequire(join(resolve(packageRoot), 'package.json'));
const { Client } = await import(pathToFileURL(require.resolve('@modelcontextprotocol/client')));
const { StdioClientTransport } = await import(pathToFileURL(require.resolve('@modelcontextprotocol/client/stdio')));
const packageInfo = JSON.parse(await readFile(join(packageRoot, 'package.json'), 'utf8'));
const client = new Client({ name: 'quietmsi-static-vendor-audit', version: '1.0.0' });
const transport = new StdioClientTransport({ command: process.execPath, args: [join(packageRoot, 'scripts/rea.mjs'), 'mcp'], stderr: 'inherit', maxBufferSize: 128 * 1024 * 1024 });
const ledger = { reaVersion: packageInfo.version, mode: 'static; targets not executed', completeReverseEngineering: false, targets: [] };
function unwrap(response) {
  if (response.isError) throw new Error(JSON.stringify(response));
  const value = response.structuredContent ?? JSON.parse(response.content.find(item => item.type === 'text').text);
  return value.result ?? value;
}
try {
  await client.connect(transport);
  const catalog = await client.listTools();
  ledger.server = client.getServerVersion();
  ledger.toolCount = catalog.tools.length;
  for (const [index, target] of targets.entries()) {
    const path = resolve(target);
    const entry = { path, observations: [], errors: [] };
    ledger.targets.push(entry);
    for (const name of ['inspect_managed_artifact', 'inspect_managed_members', 'inspect_managed_native_boundaries']) {
      if (name !== 'inspect_managed_artifact' && entry.classification !== 'managed') break;
      try {
        const response = await client.callTool({ name, arguments: { path } }, undefined, { timeout: 120_000 });
        if (response.isError) {
          const errorText = response.content.find(item => item.type === 'text')?.text;
          const error = errorText ? JSON.parse(errorText).error : undefined;
          if (error?.code === 'resource_constraint' && error.details?.resource === 'transport') {
            const filename = `session-evidence-${index}-${Date.now()}.json`;
            const exported = await client.callTool({ name: 'export_evidence_bundle', arguments: { path: join(output, filename) } }, undefined, { timeout: 120_000 });
            if (exported.isError) throw new Error(JSON.stringify(exported));
            entry.observations.push({ operation: name, file: filename, evidenceId: error.details.evidence_id, delivery: 'complete session evidence export; inline response exceeded MCP budget' });
            continue;
          }
        }
        const result = unwrap(response);
        const filename = `${index}-${basename(path)}-${name}.json`;
        await writeFile(join(output, filename), JSON.stringify(response, null, 2));
        const evidence = result.evidence ?? result;
        const normalized = evidence.normalized_result ?? result;
        entry.observations.push({ operation: name, file: filename, evidenceId: evidence.evidence_id, sha256: evidence.subject?.digest?.sha256, limitations: evidence.limitations ?? normalized.limitations });
        if (name === 'inspect_managed_artifact') entry.classification = normalized.classification?.status;
        if (name === 'inspect_managed_members') {
          entry.types = normalized.types?.length;
          entry.methods = normalized.methods?.length;
          entry.coverage = normalized.coverage;
        }
        if (name === 'inspect_managed_native_boundaries') entry.nativeBoundarySummary = normalized.summary;
      } catch (error) {
        entry.errors.push({ operation: name, message: error.message });
        break;
      }
    }
    await writeFile(join(output, 'audit-ledger.json'), JSON.stringify(ledger, null, 2));
    console.log(JSON.stringify(entry));
  }
  if (ledger.targets.some(entry => entry.errors.length)) process.exitCode = 1;
} finally {
  await client.close();
}
