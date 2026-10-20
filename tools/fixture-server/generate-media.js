'use strict';
const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');
const dir = path.join(__dirname, 'media');
fs.mkdirSync(dir, { recursive: true });
const result = spawnSync('ffmpeg', ['-hide_banner', '-y', '-f', 'lavfi', '-i', 'testsrc2=size=1280x720:rate=30', '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=48000', '-t', '12', '-c:v', 'libx264', '-profile:v', 'main', '-pix_fmt', 'yuv420p', '-b:v', '1500k', '-c:a', 'aac', '-b:a', '96k', '-movflags', '+faststart', path.join(dir, 'sample.mp4')], { stdio: 'inherit' });
if (result.error) console.error('ffmpeg must be installed and available on PATH:', result.error.message);
process.exit(result.status === null ? 1 : result.status);
