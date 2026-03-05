interface EmptyStateProps {
  icon?: string
  title: string
  desc?: string
  action?: { label: string; onClick: () => void }
}

export default function EmptyState({ icon = '📭', title, desc, action }: EmptyStateProps) {
  return (
    <div style={{
      textAlign: 'center', padding: '48px 24px',
      background: 'var(--bg-card)', borderRadius: 12,
      border: '1px solid var(--border)',
    }}>
      <div style={{ fontSize: 36, marginBottom: 12, opacity: 0.6 }}>{icon}</div>
      <div style={{ fontSize: 15, fontWeight: 700, color: 'var(--text-secondary)', marginBottom: 6 }}>{title}</div>
      {desc && <div style={{ fontSize: 12, color: 'var(--text-muted)', lineHeight: 1.6 }}>{desc}</div>}
      {action && (
        <button onClick={action.onClick} style={{
          marginTop: 16, padding: '8px 20px', borderRadius: 8, border: 'none', cursor: 'pointer',
          background: 'var(--accent-blue)', color: '#fff', fontSize: 13, fontWeight: 700,
        }}>
          {action.label}
        </button>
      )}
    </div>
  )
}
