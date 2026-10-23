'use strict';
// Repository hygiene for tracked text files: UTF-8 without BOM, LF line endings, no trailing whitespace
// (Markdown and patches excepted), a final newline and no tab indentation. Mirrors .editorconfig.
const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const textExt = new Set(['.cs', '.csproj', '.props', '.js', '.cjs', '.json', '.yml', '.yaml', '.html', '.xml', '.md', '.svg', '.gitignore', '.editorconfig']);
const relaxed = ext => ext === '.md';
const exempt = file => file.endsWith('.patch');
function check(files, base = root) {
  const problems = [];
  for (const file of files) {
    const ext = path.extname(file) || path.basename(file);
    if (!textExt.has(ext) || exempt(file)) continue;
    const buffer = fs.readFileSync(path.join(base, file));
    if (buffer.length >= 3 && buffer[0] === 0xef && buffer[1] === 0xbb && buffer[2] === 0xbf) problems.push(file + ': byte order mark');
    const text = buffer.toString('utf8');
    if (text.includes('\r')) problems.push(file + ': CR line ending');
    if (text.length && !text.endsWith('\n')) problems.push(file + ': no final newline');
    text.split('\n').forEach((line, i) => {
      if (!relaxed(ext) && /[ \t]+$/.test(line)) problems.push(`${file}:${i + 1}: trailing whitespace`);
      if (ext !== '.md' && ext !== '.svg' && /^\t+/.test(line)) problems.push(`${file}:${i + 1}: tab indentation`);
    });
  }
  return problems;
}
if (require.main === module) {
  const files = execFileSync('git', ['ls-files'], { cwd: root, encoding: 'utf8' }).split('\n').filter(Boolean);
  const problems = check(files);
  if (problems.length) { console.error(problems.slice(0, 40).join('\n') + (problems.length > 40 ? `\n... ${problems.length - 40} more` : '')); process.exitCode = 1; }
  else console.log('PASS style: tracked text files are UTF-8, LF, final newline, no trailing whitespace or tab indentation');
}
module.exports = { check };
