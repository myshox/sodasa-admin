interface EmptyStateProps {
  icon?: string
  title: string
  desc?: string
  action?: { label: string; onClick: () => void }
}

export default function EmptyState({ icon = '📭', title, desc, action }: EmptyStateProps) {
  return (
    <div className="ui-empty-state" role="status" aria-label={title}>
      <div style={{ fontSize: 36, marginBottom: 12, opacity: 0.65, filter: 'grayscale(0.15)' }} aria-hidden>{icon}</div>
      <div style={{ fontSize: 15, fontWeight: 800, color: 'var(--text-secondary)', marginBottom: 6, letterSpacing: '-0.02em' }}>{title}</div>
      {desc && <div className="ui-hint" style={{ maxWidth: 360, margin: '0 auto' }}>{desc}</div>}
      {action && (
        <button type="button" className="primary" onClick={action.onClick} style={{ marginTop: 18 }}>
          {action.label}
        </button>
      )}
    </div>
  )
}
