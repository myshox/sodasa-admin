import { useState } from 'react'
import api from '../api'
import { S } from '../strings'

export default function PetCmdPage() {
  const [searchQ, setSearchQ] = useState('')
  const [cdkey,   setCdkey]   = useState('')
  const [charName,setChar]    = useState('')
  const [petId,   setPetId]   = useState(1)
  const [useCdkey,setUseCdkey]= useState(false)
  const [mkLv,    setMkLv]    = useState(1)
  const [mkReb,   setMkReb]   = useState(0)
  const [hp,      setHp]      = useState(1000)
  const [atk,     setAtk]     = useState(200)
  const [def,     setDef]     = useState(100)
  const [spd,     setSpd]     = useState(100)
  const [abiLv,   setAbiLv]   = useState(1)
  const [abiReb,  setAbiReb]  = useState(0)
  const [copied,  setCopied]  = useState('')

  const mkCmd  = `[gm petmake ${petId} ${mkLv} ${mkReb}${useCdkey && cdkey ? ` ${cdkey}` : ''}]`
  const abiCmd = `[gm petmakeabi ${petId} ${hp} ${atk} ${def} ${spd} ${abiLv} ${abiReb}]`

  const copy = (text: string, key: string) => {
    navigator.clipboard.writeText(text)
    setCopied(key); setTimeout(() => setCopied(''), 1500)
  }

  const searchPlayer = async () => {
    if (!searchQ.trim()) return
    try {
      const r = await api.get(`/players/${searchQ.trim()}`)
      setCdkey(r.data.account)
      setChar(r.data.onlineName || r.data.account)
    } catch { alert('找不到玩家') }
  }

  const Nud = ({ label, value, onChange, min = 0, max = 99999 }: {
    label: string; value: number; onChange: (v: number) => void; min?: number; max?: number
  }) => (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
      <span style={{ width: 90, color: 'var(--text-muted)', fontSize: 13, textAlign: 'right', flexShrink: 0 }}>
        {label}
      </span>
      <button onClick={() => onChange(Math.max(min, value - 1))}
        style={{ background: 'var(--bg-input)', color: 'var(--text-secondary)', padding: '4px 12px', border: '1px solid var(--border)' }}>
        {S.minus}
      </button>
      <input type="number" value={value}
        onChange={e => onChange(Math.min(max, Math.max(min, +e.target.value || 0)))}
        style={{ width: 100, textAlign: 'center' }} />
      <button onClick={() => onChange(Math.min(max, value + 1))}
        style={{ background: 'var(--bg-input)', color: 'var(--text-secondary)', padding: '4px 12px', border: '1px solid var(--border)' }}>
        {S.plus}
      </button>
    </div>
  )

  const Card = ({ title, children }: { title: string; children: React.ReactNode }) => (
    <div style={{
      background: 'var(--bg-card)', border: '1px solid var(--border)',
      borderRadius: 10, padding: 20, marginBottom: 16
    }}>
      <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 14 }}>{title}</h3>
      {children}
    </div>
  )

  const CmdBar = ({ cmd, ckey }: { cmd: string; ckey: string }) => (
    <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 12 }}>
      <input readOnly value={cmd} style={{
        flex: 1, background: '#0d1a0d', color: '#6eff8a',
        fontFamily: 'Consolas, monospace', fontSize: 13, fontWeight: 600
      }} />
      <button onClick={() => copy(cmd, ckey)} style={{
        background: copied === ckey ? 'var(--accent-green)' : 'var(--accent-blue)',
        color: '#fff', padding: '6px 14px', fontSize: 13, flexShrink: 0
      }}>
        {copied === ckey ? S.copied : S.copy}
      </button>
    </div>
  )

  return (
    <div style={{ padding: 28, maxWidth: 660 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>
        🐾 {S.pagePetCmd}
      </h1>

      <Card title={`👤 ${S.petTarget}`}>
        <div style={{ display: 'flex', gap: 8 }}>
          <input value={searchQ} onChange={e => setSearchQ(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && searchPlayer()}
            placeholder={S.petTargetPlh} style={{ flex: 1 }} />
          <button onClick={searchPlayer}
            style={{ background: 'var(--accent-blue)', color: '#fff', flexShrink: 0 }}>
            🔍 {S.petQuery}
          </button>
        </div>
        {cdkey && (
          <p style={{ marginTop: 8, fontSize: 13, color: 'var(--accent-green)' }}>
            {S.petFound(charName, cdkey)}
          </p>
        )}
      </Card>

      <Card title={`🔒 ${S.petIdLabel}`}>
        <Nud label={S.petIdLabel} value={petId} onChange={setPetId} min={1} />
      </Card>

      <Card title={`⚙️ ${S.petMkTitle}`}>
        <Nud label={S.petLevel}   value={mkLv}  onChange={setMkLv}  min={1} max={200} />
        <Nud label={S.petRebirth} value={mkReb} onChange={setMkReb} min={0} max={20} />
        <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontSize: 13, color: 'var(--text-secondary)', marginTop: 6 }}>
          <input type="checkbox" checked={useCdkey} onChange={e => setUseCdkey(e.target.checked)} />
          {S.petCdkey}
        </label>
        {useCdkey && (
          <input value={cdkey} onChange={e => setCdkey(e.target.value)}
            placeholder={S.petCdkeyPlh} style={{ width: '100%', marginTop: 8 }} />
        )}
        <CmdBar cmd={mkCmd} ckey="mk" />
      </Card>

      <Card title={`⚙️ ${S.petAbiTitle}`}>
        <Nud label={S.petHp}      value={hp}     onChange={setHp}     min={1} />
        <Nud label={S.petAtk}     value={atk}    onChange={setAtk} />
        <Nud label={S.petDef}     value={def}    onChange={setDef} />
        <Nud label={S.petSpd}     value={spd}    onChange={setSpd} />
        <Nud label={S.petLevel}   value={abiLv}  onChange={setAbiLv}  min={1} max={200} />
        <Nud label={S.petRebirth} value={abiReb} onChange={setAbiReb} min={0} max={20} />
        <CmdBar cmd={abiCmd} ckey="abi" />
        <p style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 8 }}>{S.petAbiNote}</p>
      </Card>
    </div>
  )
}
