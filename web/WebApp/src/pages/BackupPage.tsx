import { useState } from 'react'
import api from '../api'
import { S } from '../strings'

export default function BackupPage() {
  const [loading, setLoading] = useState(false)
  const [msg, setMsg] = useState('')

  const downloadBackup = async () => {
    setLoading(true)
    setMsg('')
    try {
      const r = await api.get('/backup/export', { responseType: 'blob' })
      const url = URL.createObjectURL(r.data)
      const a = document.createElement('a')
      a.href = url
      a.download = `backup_${new Date().toISOString().slice(0, 19).replace(/[-:T]/g, '')}.sql`
      a.click()
      URL.revokeObjectURL(url)
      setMsg('✓ 備份檔已下載（csalogin + lock，INSERT IGNORE 格式）。還原請使用 EXE 或伺服器端執行 SQL。')
    } catch (e) {
      setMsg('下載失敗，請確認 API 已啟動且資料庫連線正常。')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="gm-page-stack gm-max-sm">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>💾 {S.navBackup}</h1>
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 24, marginBottom: 20 }}>
        <p style={{ marginBottom: 12, color: 'var(--text-secondary)' }}>備份內容：<strong>csalogin</strong>（玩家帳號）+ <strong>lock</strong>（封禁記錄）</p>
        <p style={{ marginBottom: 16, fontSize: 13, color: 'var(--text-muted)' }}>格式：SQL 文字檔（INSERT IGNORE）。還原時不覆蓋現有資料，只補回遺失記錄。</p>
        <button onClick={downloadBackup} disabled={loading}
          style={{ padding: '12px 24px', background: 'var(--accent-blue)', color: '#fff', borderRadius: 8, fontWeight: 600 }}>
          {loading ? '產生中…' : '📥 下載備份'}
        </button>
      </div>
      {msg && <p style={{ color: msg.startsWith('✓') ? 'var(--accent-green)' : 'var(--accent-red)', fontSize: 13 }}>{msg}</p>}
      <p style={{ marginTop: 24, fontSize: 12, color: 'var(--text-muted)' }}>還原：請使用 EXE 版「備份還原」選擇檔案還原，或於 MySQL 端執行下載的 .sql 檔案。</p>
    </div>
  )
}
