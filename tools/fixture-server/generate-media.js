'use strict';
const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');
const dir = path.join(__dirname, 'media');
fs.mkdirSync(dir, { recursive: true });
const result = spawnSync('ffmpeg', ['-hide_banner', '-y', '-f', 'lavfi', '-i', 'testsrc2=size=1280x720:rate=30', '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=48000', '-t', '12', '-c:v', 'libx264', '-profile:v', 'main', '-pix_fmt', 'yuv420p', '-b:v', '1500k', '-c:a', 'aac', '-b:a', '96k', '-movflags', '+faststart', path.join(dir, 'sample.mp4')], { stdio: 'inherit' });
if (result.error) console.error('ffmpeg must be installed and available on PATH:', result.error.message);
if (result.status !== 0) process.exit(result.status === null ? 1 : result.status);
// Fragmented copy (same samples) for Media Source Extensions tests.
const frag = spawnSync('ffmpeg', ['-hide_banner', '-y', '-i', path.join(dir, 'sample.mp4'), '-c', 'copy', '-movflags', 'frag_keyframe+empty_moov+default_base_moof', path.join(dir, 'sample-frag.mp4')], { stdio: 'inherit' });
if (frag.status !== 0) process.exit(frag.status === null ? 1 : frag.status);
// Audio-only AAC and software-decodable VP9/Opus clips: they separate codec support from the playback path in device probes.
const audio = spawnSync('ffmpeg', ['-hide_banner', '-y', '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=48000', '-t', '8', '-c:a', 'aac', '-b:a', '96k', path.join(dir, 'sample.m4a')], { stdio: 'inherit' });
if (audio.status !== 0) process.exit(audio.status === null ? 1 : audio.status);
const vp9 = spawnSync('ffmpeg', ['-hide_banner', '-y', '-f', 'lavfi', '-i', 'testsrc2=size=640x360:rate=30', '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=48000', '-t', '6', '-c:v', 'libvpx-vp9', '-b:v', '600k', '-c:a', 'libopus', path.join(dir, 'sample-vp9.webm')], { stdio: 'inherit' });
if (vp9.status !== 0) process.exit(vp9.status === null ? 1 : vp9.status);
// A short HLS (H.264/AAC, 4 s segments) so a device run can exercise manifest detection and native playback.
const hlsDir = path.join(dir, 'hls');
fs.mkdirSync(hlsDir, { recursive: true });
const hls = spawnSync('ffmpeg', ['-hide_banner', '-y', '-i', path.join(dir, 'sample.mp4'), '-c', 'copy', '-f', 'hls', '-hls_time', '4', '-hls_playlist_type', 'vod', '-hls_segment_filename', path.join(hlsDir, 'seg%03d.ts'), path.join(hlsDir, 'index.m3u8')], { stdio: 'inherit' });
process.exit(hls.status === null ? 1 : hls.status);
