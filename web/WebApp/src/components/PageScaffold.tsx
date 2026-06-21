import type { ReactNode } from 'react'

const MAX: Record<'default' | 'sm' | 'md' | 'lg' | 'xl', string> = {
  default: '',
  sm: 'gm-max-sm',
  md: 'gm-max-md',
  lg: 'gm-max-lg',
  xl: 'gm-max-xl',
}

type Props = {
  title?: string
  subtitle?: string
  icon?: string
  max?: keyof typeof MAX
  className?: string
  children: ReactNode
}

/**
 * 全站子頁統一外層：與 index.css `.gm-page-stack` 搭配。
 * 若頁面已有自己的 h1，可不傳 title，僅用 className="gm-page-stack"。
 */
export default function PageScaffold({ title, subtitle, icon, max = 'default', className, children }: Props) {
  const mc = MAX[max]
  return (
    <div className={`gm-page-stack ${mc} ${className ?? ''}`.trim()}>
      {(title || subtitle) && (
        <header className="gm-page-header">
          {title && (
            <h1 className="gm-page-title">
              {icon ? <span className="gm-page-icon" aria-hidden>{icon}</span> : null}
              {title}
            </h1>
          )}
          {subtitle ? <p className="gm-page-subtitle">{subtitle}</p> : null}
        </header>
      )}
      {children}
    </div>
  )
}
