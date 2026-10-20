# Isko artwork

Canonical artwork: src/Isko.Tv/shared/res/isko.png. The logo uses an i/open-loop/play
shape in violet and blue, on a transparent background. Pixel content is retained;
non-pixel provenance metadata is omitted from this source distribution.

The default manifest references isko.png under shared/res. Keep aspect ratio and
clear space, and decode at the intended UI size. UiIcons.cs draws the matching
wordmark/cursor and UI symbols. The Lab variant falls back to the wordmark.
Small-size legibility and native rendering require their own visual verification.
