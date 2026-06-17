/**
 * Temporary landing/test page.
 * Its only purpose right now is to confirm the toolchain works:
 * React + TypeScript render, and Tailwind utility classes apply.
 * Real pages (Login, Register, Dashboard) are built in later sessions.
 */
function App() {
  return (
    <div className="min-h-screen flex flex-col items-center justify-center gap-6 bg-slate-50 text-[#1E2A44]">
      {/* Brand logo served from public/logo.svg */}
      <img src="/logo.svg" alt="TaskPilot" className="w-64" />

      <h1 className="text-3xl font-bold tracking-tight">
        TaskPilot frontend is ready
      </h1>

      <p className="text-slate-500">
        React + TypeScript + Tailwind CSS are wired up.
      </p>

      {/* The yellow accent button verifies Tailwind colors/hover work */}
      <button
        type="button"
        className="rounded-lg bg-[#F6BE2C] px-6 py-2 font-semibold text-[#1E2A44] shadow transition hover:brightness-95"
      >
        It works
      </button>
    </div>
  )
}

export default App
