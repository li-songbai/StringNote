(() => {
  'use strict';

  // ─── Chord Fingering Library (6th string E -> 1st string e) ────────────────
  const CHORD_SHAPES = {
    C:   { frets: [null, 3, 2, 0, 1, 0], baseFret: 1 },
    G:   { frets: [3, 2, 0, 0, 0, 3],    baseFret: 1 },
    Am:  { frets: [null, 0, 2, 2, 1, 0], baseFret: 1 },
    Em:  { frets: [0, 2, 2, 0, 0, 0],    baseFret: 1 },
    F:   { frets: [1, 3, 3, 2, 1, 1],    baseFret: 1, barre: 1 },
    Dm:  { frets: [null, null, 0, 2, 3, 1], baseFret: 1 },
    D:   { frets: [null, null, 0, 2, 3, 2], baseFret: 1 },
    Bm:  { frets: [null, 2, 4, 4, 3, 2], baseFret: 1, barre: 2 },
    A:   { frets: [null, 0, 2, 2, 2, 0], baseFret: 1 },
    Bb:  { frets: [null, 1, 3, 3, 3, 1], baseFret: 1, barre: 1 },
    Gm:  { frets: [3, 5, 5, 3, 3, 3],    baseFret: 3, barre: 3 },
    'F#m': { frets: [2, 4, 4, 2, 2, 2],  baseFret: 2, barre: 2 }
  };

  // Transpose map for our sample progression (I - V - vi - iii - IV - I - ii - V)
  const KEY_PROFILES = {
    C: {
      label: 'C 调',
      capo: '不夹',
      chords: ['C', 'G', 'Am', 'Em', 'F', 'C', 'Dm', 'G']
    },
    G: {
      label: 'G 调',
      capo: '夹 5 品',
      chords: ['G', 'D', 'Em', 'Bm', 'C', 'G', 'Am', 'D']
    },
    F: {
      label: 'F 调',
      capo: '夹 7 品',
      chords: ['F', 'C', 'Dm', 'Am', 'Bb', 'F', 'Gm', 'C']
    },
    D: {
      label: 'D 调',
      capo: '夹 2 品',
      chords: ['D', 'A', 'Bm', 'F#m', 'G', 'D', 'Em', 'A']
    }
  };

  // Sample original public-domain / folk-style lyric lines with chord anchor indices
  const SAMPLE_LINES = [
    {
      text: '晚风吹过旧窗台，木吉他轻轻响起来',
      alt:  '灯光落在乐谱纸，和弦陪夜色慢慢开',
      anchors: [
        { pos: 0, chordIdx: 0 },
        { pos: 4, chordIdx: 1 },
        { pos: 8, chordIdx: 2 },
        { pos: 13, chordIdx: 3 }
      ]
    },
    {
      text: '把每段旋律写下来，留给明天的自己听',
      alt:  '把每行词句对齐好，一页纸装下整首歌',
      anchors: [
        { pos: 0, chordIdx: 4 },
        { pos: 5, chordIdx: 5 },
        { pos: 9, chordIdx: 6 },
        { pos: 14, chordIdx: 7 }
      ]
    }
  ];

  const state = {
    key: 'C',
    mode: 'name', // 'name' | 'diagram'
    stacked: true
  };

  function renderChordDiagramSvg(chordName) {
    const shape = CHORD_SHAPES[chordName];
    if (!shape) {
      return `<span class="chord-name-tag">${chordName}</span>`;
    }
    const base = shape.baseFret || 1;
    const width = 44;
    const height = 54;
    const left = 7;
    const top = 19;
    const colGap = 6;
    const rowGap = 7.5;

    let svg = `<svg class="chord-diagram-svg" viewBox="0 0 ${width} ${height}" aria-label="${chordName} 指法图">`;
    // Chord name
    svg += `<text x="${width / 2}" y="10" text-anchor="middle" font-family="JetBrains Mono, Consolas, monospace" font-size="9.5" font-weight="700" fill="#2F6B48">${chordName}</text>`;

    // Nut or base fret indicator
    if (base === 1) {
      svg += `<line x1="${left}" y1="${top}" x2="${left + colGap * 5}" y2="${top}" stroke="#1B2620" stroke-width="2" />`;
    } else {
      svg += `<line x1="${left}" y1="${top}" x2="${left + colGap * 5}" y2="${top}" stroke="#8FA097" stroke-width="1" />`;
      svg += `<text x="2.5" y="${top + 6.5}" font-family="JetBrains Mono, monospace" font-size="6.5" fill="#66776E">${base}</text>`;
    }

    // Horizontal fret lines (4 frets)
    for (let r = 1; r <= 4; r++) {
      const y = top + r * rowGap;
      svg += `<line x1="${left}" y1="${y}" x2="${left + colGap * 5}" y2="${y}" stroke="#CDD7D1" stroke-width="0.9" />`;
    }

    // Vertical string lines (6 strings)
    for (let s = 0; s < 6; s++) {
      const x = left + s * colGap;
      svg += `<line x1="${x}" y1="${top}" x2="${x}" y2="${top + rowGap * 4}" stroke="#8FA097" stroke-width="0.85" />`;
    }

    // Open / Mute markers and finger dots
    shape.frets.forEach((fret, sIdx) => {
      const x = left + sIdx * colGap;
      if (fret === null) {
        svg += `<text x="${x}" y="${top - 3}" text-anchor="middle" font-size="7" fill="#8FA097">×</text>`;
      } else if (fret === 0) {
        svg += `<circle cx="${x}" cy="${top - 5}" r="2" fill="none" stroke="#66776E" stroke-width="0.9" />`;
      } else {
        const relFret = fret - base + 1;
        if (relFret >= 1 && relFret <= 4) {
          const cy = top + (relFret - 0.5) * rowGap;
          svg += `<circle cx="${x}" cy="${cy}" r="2.5" fill="#2F6B48" />`;
        }
      }
    });

    svg += '</svg>';
    return svg;
  }

  function renderPreviewSheet() {
    const container = document.getElementById('interactive-lyrics');
    const keyTag = document.getElementById('sheet-key-tag');
    const capoTag = document.getElementById('sheet-capo-tag');
    if (!container) return;

    const profile = KEY_PROFILES[state.key] || KEY_PROFILES.C;
    if (keyTag) keyTag.textContent = `选调 ${profile.label}`;
    if (capoTag) capoTag.textContent = `变调夹 ${profile.capo}`;

    const html = SAMPLE_LINES.map((line) => {
      const chars = Array.from(line.text);
      const altChars = Array.from(line.alt);
      const anchorMap = new Map();
      line.anchors.forEach((a) => {
        anchorMap.set(a.pos, profile.chords[a.chordIdx]);
      });

      const cells = chars.map((ch, idx) => {
        const chord = anchorMap.get(idx);
        const chordMarkup = chord
          ? (state.mode === 'diagram'
              ? renderChordDiagramSvg(chord)
              : `<span class="chord-name-tag">${chord}</span>`)
          : '';
        const altMarkup = state.stacked
          ? `<span class="lyric-char-alt">${altChars[idx] ?? ''}</span>`
          : '';

        return `
          <span class="lyric-cell">
            <span class="lyric-chord-slot ${state.mode === 'diagram' ? 'diagram-mode' : ''}">${chordMarkup}</span>
            <span class="lyric-char">${ch}</span>
            ${altMarkup}
          </span>
        `;
      }).join('');

      return `<div class="lyric-line">${cells}</div>`;
    }).join('');

    container.innerHTML = html;
  }

  function bindSheetControls() {
    document.querySelectorAll('[data-key]').forEach((btn) => {
      btn.addEventListener('click', () => {
        state.key = btn.getAttribute('data-key') || 'C';
        document.querySelectorAll('[data-key]').forEach((b) => b.classList.toggle('active', b === btn));
        renderPreviewSheet();
      });
    });

    const modeBtn = document.getElementById('toggle-chord-mode');
    if (modeBtn) {
      modeBtn.addEventListener('click', () => {
        state.mode = state.mode === 'name' ? 'diagram' : 'name';
        modeBtn.classList.toggle('active', state.mode === 'diagram');
        modeBtn.textContent = state.mode === 'diagram' ? '指法图：开' : '指法图：关';
        renderPreviewSheet();
      });
    }

    const stackBtn = document.getElementById('toggle-stacked-lyrics');
    if (stackBtn) {
      stackBtn.addEventListener('click', () => {
        state.stacked = !state.stacked;
        stackBtn.classList.toggle('active', state.stacked);
        stackBtn.textContent = state.stacked ? '叠行歌词：开' : '叠行歌词：关';
        renderPreviewSheet();
      });
    }
  }

  // ─── Fetch Latest GitHub Release (supports pre-releases) ───────────────────
  async function loadLatestRelease() {
    const versionEls = document.querySelectorAll('[data-latest-version]');
    const setupLink = document.getElementById('download-setup-btn');
    const portableLink = document.getElementById('download-portable-btn');
    const heroDownloadBtn = document.getElementById('hero-download-btn');
    const releaseMeta = document.getElementById('release-meta-text');

    try {
      const resp = await fetch('https://api.github.com/repos/li-songbai/StringNote/releases?per_page=1', {
        headers: { Accept: 'application/vnd.github+json' }
      });
      if (!resp.ok) return;
      const releases = await resp.json();
      if (!Array.isArray(releases) || releases.length === 0) return;

      const latest = releases[0];
      const tag = latest.tag_name || 'v0.1.0-beta.8';
      const published = latest.published_at ? latest.published_at.slice(0, 10) : '';
      const assets = Array.isArray(latest.assets) ? latest.assets : [];

      const setupAsset = assets.find((a) => /Setup-.*\.exe$/i.test(a.name));
      const portableAsset = assets.find((a) => /Portable-.*\.zip$/i.test(a.name));

      versionEls.forEach((el) => {
        el.textContent = tag;
      });

      if (setupAsset && setupLink) {
        setupLink.href = setupAsset.browser_download_url;
      }
      if (setupAsset && heroDownloadBtn) {
        heroDownloadBtn.href = setupAsset.browser_download_url;
      }
      if (portableAsset && portableLink) {
        portableLink.href = portableAsset.browser_download_url;
      }
      if (releaseMeta) {
        const datePart = published ? ` · 发布于 ${published}` : '';
        releaseMeta.textContent = `当前最新版本：${tag}${datePart}（支持应用内一键检查更新）`;
      }
    } catch {
      // Fallback to static release links already present in HTML
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    bindSheetControls();
    renderPreviewSheet();
    loadLatestRelease();
  });
})();
