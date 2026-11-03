/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,jsx}'],
  theme: {
    extend: {
      colors: {
        canvas: '#0d1117',
        panel: '#161b22',
        raised: '#1c232d',
        line: '#30363d',
        ink: '#e6edf3',
        mute: '#8b949e',
        fox: { DEFAULT: '#ff7a1a', soft: '#ff9a4d', deep: '#c75a0a' },
        ok: '#3fb950',
        bad: '#f85149',
        warn: '#d29922',
      },
      boxShadow: { neon: '0 0 0 1px rgba(255,122,26,.5), 0 0 18px rgba(255,122,26,.25)' },
      fontFamily: { sans: ['Inter', 'system-ui', 'sans-serif'], mono: ['JetBrains Mono', 'ui-monospace', 'monospace'] },
    },
  },
  plugins: [],
};
