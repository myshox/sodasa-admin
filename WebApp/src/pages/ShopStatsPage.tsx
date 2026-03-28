import { useMemo, useState, useEffect } from 'react'
import api from '../api'
import { S } from '../strings'

const SHOPS = [
  { id: 'vipshop', label: '金幣商店', icon: '💰', unit: '金幣' },
  { id: 'fameshop', label: '聲望商店', icon: '🏆', unit: '聲望' },
  { id: 'csshopnum', label: '石壁商店', icon: '🪨', unit: '石壁' },
  { id: 'csxsshopnum', label: '戰點商店', icon: '⚔', unit: '戰點' },
] as const

type Preset = 'all' | 'd7' | 'd30' | 'month' | 'custom'

function ymdLocal(d: Date): string {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

function rangeFromPreset(p: Preset, customFrom: string, customTo: string): { from?: string; to?: string; label: string } {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  if (p === 'all') return { label: '全時段（累計）' }
  if (p === 'd7') {
    const a = new Date(today)
    a.setDate(a.getDate() - 6)
    return { from: ymdLocal(a), to: ymdLocal(today), label: '最近 7 天' }
  }
  if (p === 'd30') {
    const a = new Date(today)
    a.setDate(a.getDate() - 29)
    return { from: ymdLocal(a), to: ymdLocal(today), label: '最近 30 天' }
  }
  if (p === 'month') {
    const a = new Date(today.getFullYear(), today.getMonth(), 1)
    return { from: ymdLocal(a), to: ymdLocal(today), label: '本月至今' }
  }
  if (customFrom && customTo) {
    return { from: customFrom, to: customTo, label: `${customFrom} ～ ${customTo}` }
  }
  return { label: '自訂（請選擇起訖日）' }
}

export default function ShopStatsPage() {
  const [tab, setTab] = useState<string>('vipshop')
  const [preset, setPreset] = useState<Preset>('d30')
  const [customFrom, setCustomFrom] = useState(() => ymdLocal(new Date(Date.now() - 29 * 86400000)))
  const [customTo, setCustomTo] = useState(() => ymdLocal(new Date()))
  const [data, setData] = useState<{ items: any[]; spenders: any[] } | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const q = useMemo(() => rangeFromPreset(preset, customFrom, customTo), [preset, customFrom, customTo])

  useEffect(() => {
    if (preset === 'custom' && (!customFrom || !customTo)) {
      setLoading(false)
      setData(null)
      return
    }
    setLoading(true)
    setError(null)
    const params: Record<string, string | number> = { top: 20 }
    if (q.from) params.from = q.from
    if (q.to) params.to = q.to
    api
      .get(`/shop/${tab}`, { params })
      .then(r => {
        setData(r.data)
        setLoading(false)
      })
      .catch((e: unknown) => {
        const msg =
          (e as { response?: { data?: { message?: string } } })?.response?.data?.message ||
          (e as { message?: string })?.message ||
          '無法載入商城統計，請稍後再試或檢查後端連線。'
        setError(msg)
        setData(null)
        setLoading(false)
      })
  }, [tab, preset, customFrom, customTo, q])

  const isCsShop = tab === 'csshopnum' || tab === 'csxsshopnum'

  return (
    <div className="gm-page-stack shop-stats-page">
      <div className="shop-stats-page__hero">
        <h1>🏪 {S.navShop}</h1>
        <p className="shop-stats-page__sub">
          依交易日期篩選熱賣道具與消費排行（金幣／聲望商店含玩家排行；石壁／戰點表僅道具維度）。
        </p>
      </div>

      <div className="shop-stats-page__toolbar">
        <div className="shop-stats-page__shops">
          {SHOPS.map(s => (
            <button
              key={s.id}
              type="button"
              className={`shop-stats-page__shop-btn${tab === s.id ? ' shop-stats-page__shop-btn--on' : ''}`}
              onClick={() => setTab(s.id)}
            >
              {s.icon} {s.label}
            </button>
          ))}
        </div>
        <div className="shop-stats-page__dates">
          <span className="shop-stats-page__dates-label">統計區間</span>
          <select
            className="shop-stats-page__select"
            value={preset}
            onChange={e => setPreset(e.target.value as Preset)}
            aria-label="統計區間"
          >
            <option value="all">全部（累計）</option>
            <option value="d7">最近 7 天</option>
            <option value="d30">最近 30 天</option>
            <option value="month">本月至今</option>
            <option value="custom">自訂…</option>
          </select>
          {preset === 'custom' && (
            <>
              <input
                type="date"
                className="shop-stats-page__input-date"
                value={customFrom}
                onChange={e => setCustomFrom(e.target.value)}
              />
              <span className="shop-stats-page__dash">～</span>
              <input
                type="date"
                className="shop-stats-page__input-date"
                value={customTo}
                onChange={e => setCustomTo(e.target.value)}
              />
            </>
          )}
          <span className="shop-stats-page__range-hint">{q.label}</span>
        </div>
      </div>

      {loading ? (
        <p style={{ color: 'var(--text-muted)' }}>載入中…</p>
      ) : error ? (
        <div className="ui-error" role="alert">
          {error}
        </div>
      ) : data ? (
        <div className="shop-stats-page__panels">
          <section className="shop-stats-page__card">
            <h2 className="shop-stats-page__card-title">熱賣道具 Top 20</h2>
            <div className="shop-stats-page__table-wrap table-wrap">
              <div className="shop-stats-page__thead shop-stats-page__thead--items">
                <span>排名</span>
                <span>道具ID</span>
                <span>名稱</span>
                <span>數量</span>
                <span>筆數</span>
                <span>消耗</span>
                <span>最後購買</span>
              </div>
              {data.items.length === 0 ? (
                <p className="shop-stats-page__empty">此區間尚無購買記錄</p>
              ) : (
                data.items.map((row: any) => (
                  <div key={`${row.rank}-${row.itemId}`} className="shop-stats-page__row shop-stats-page__row--items">
                    <span className="shop-stats-page__rank">{row.rank}</span>
                    <span>{row.itemId}</span>
                    <span>{row.itemName}</span>
                    <span>{row.totalQty?.toLocaleString?.() ?? row.totalQty}</span>
                    <span>{row.orderCount}</span>
                    <span>{row.totalCost != null ? row.totalCost.toLocaleString?.() ?? row.totalCost : '—'}</span>
                    <span className="shop-stats-page__muted">{row.lastTime || '—'}</span>
                  </div>
                ))
              )}
            </div>
          </section>

          <section className="shop-stats-page__card">
            <h2 className="shop-stats-page__card-title">消費排行 Top 20</h2>
            {isCsShop && (
              <p className="shop-stats-page__note">此商店資料表無帳號／角色欄位，僅顯示道具熱賣（上方）。</p>
            )}
            <div className="shop-stats-page__table-wrap table-wrap">
              <div className="shop-stats-page__thead shop-stats-page__thead--spend">
                <span>排名</span>
                <span>帳號</span>
                <span>角色</span>
                <span>數量</span>
                <span>消耗</span>
              </div>
              {data.spenders.length === 0 ? (
                <p className="shop-stats-page__empty">{isCsShop ? '—' : '此區間尚無消費記錄'}</p>
              ) : (
                data.spenders.map((row: any) => (
                  <div key={`${row.rank}-${row.cdkey}`} className="shop-stats-page__row shop-stats-page__row--spend">
                    <span className="shop-stats-page__rank">{row.rank}</span>
                    <span>{row.cdkey}</span>
                    <span>{row.name}</span>
                    <span>{row.totalQty?.toLocaleString?.() ?? row.totalQty}</span>
                    <span>{row.totalCost?.toLocaleString?.() ?? row.totalCost}</span>
                  </div>
                ))
              )}
            </div>
          </section>
        </div>
      ) : (
        <p style={{ color: 'var(--text-muted)' }}>尚無資料</p>
      )}
    </div>
  )
}
