import { useState } from 'react'
import { S } from '../strings'
import PlayerAutocomplete from '../components/PlayerAutocomplete'
import ItemAutocomplete from '../components/ItemAutocomplete'
import type { ItemInfo } from '../components/ItemBrowser'
import type { PlayerRow } from '../api'

export default function PetCmdPage() {
  const [cdkey,         setCdkey]        = useState('')
  const [charName,      setChar]         = useState('')
  const [playerQ,       setPlayerQ]      = useState('')
  const [pickedPlayers, setPickedPlayers]= useState<PlayerRow[]>([])
  const [petId,    setPetId]   = useState(1)
  const [petName,  setPetName] = useState('')
  const [useCdkey, setUseCdkey]= useState(false)
  const [mkLv,     setMkLv]    = useState(1)
  const [mkReb,    setMkReb]   = useState(0)

  // ── 直接輸入（原始）
  const [hp,       setHp]      = useState(1000)
  const [atk,      setAtk]     = useState(200)
  const [def,      setDef]     = useState(100)
  const [spd,      setSpd]     = useState(100)
  const [abiLv,    setAbiLv]   = useState(140)
  const [abiReb,   setAbiReb]  = useState(1)

  // ── 目標面板數值反推
  const [tgtHp,  setTgtHp]  = useState(2000)
  const [tgtAtk, setTgtAtk] = useState(450)
  const [tgtDef, setTgtDef] = useState(290)
  const [tgtAgi, setTgtAgi] = useState(310)

  // ── 精準三圍反推（直接輸入成長率）
  const [grHp,  setGrHp]  = useState(2050)
  const [grAtk, setGrAtk] = useState(3.1)
  const [grDef, setGrDef] = useState(2.1)
  const [grAgi, setGrAgi] = useState(2.0)

  const [copied, setCopied] = useState('')

  // 多角色：每人一行指令
  const mkCmd = useCdkey && pickedPlayers.length > 1
    ? pickedPlayers.map(p => `[gm petmake ${petId} ${mkLv} ${mkReb} ${p.account}]`).join('\n')
    : `[gm petmake ${petId} ${mkLv} ${mkReb}${useCdkey && cdkey ? ` ${cdkey}` : ''}]`
  const abiCmd = `[gm petmakeabi ${petId} ${hp} ${atk} ${def} ${spd} ${abiLv} ${abiReb}]`

  // 目標面板數值反推計算
  const convHp  = Math.round(tgtHp  / 0.0764)
  const convAtk = Math.round(tgtAtk * 1.08)
  const convDef = Math.round(tgtDef * 0.95)
  const convAgi = tgtAgi
  const abiCmd2 = `[gm petmakeabi ${petId} ${convHp} ${convAtk} ${convDef} ${convAgi} 140 1]`
  const predAtk   = ((tgtAtk - 19) / 139)
  const predDef   = ((tgtDef - 12) / 139)
  const predAgiV  = ((tgtAgi - 12) / 139)
  const predTotal = predAtk + predDef + predAgiV

  // 精準三圍反推計算（兩段式）
  const grTgtAtk = Math.round(grAtk * 139 + 19)
  const grTgtDef = Math.round(grDef * 139 + 12)
  const grTgtAgi = Math.round(grAgi * 139 + 12)
  const grInpHp  = Math.round(grHp  / 0.0764)
  const grInpAtk = Math.round(grTgtAtk * 1.08)
  const grInpDef = Math.round(grTgtDef * 0.95)
  const grTotal  = grAtk + grDef + grAgi
  const abiCmd3  = `[gm petmakeabi ${petId} ${grInpHp} ${grInpAtk} ${grInpDef} ${grTgtAgi} 140 1]`

  const copy = (text: string, key: string) => {
    navigator.clipboard.writeText(text)
    setCopied(key); setTimeout(() => setCopied(''), 1500)
  }

  const onSelectPlayer = (p: PlayerRow) => {
    setPickedPlayers([p])
    setCdkey(p.account)
    setChar(p.onlineName || p.account)
    setPlayerQ(p.onlineName || p.account)
  }

  const onSelectMultiPlayer = (players: PlayerRow[]) => {
    setPickedPlayers(players)
    if (players.length === 1) {
      setCdkey(players[0].account)
      setChar(players[0].onlineName || players[0].account)
      setPlayerQ(players[0].onlineName || players[0].account)
    } else {
      setCdkey(players[0].account)
      setChar(players[0].onlineName || players[0].account)
      setPlayerQ(`已選取 ${players.length} 個角色`)
    }
  }

  const onSelectPet = (item: ItemInfo) => {
    setPetId(item.id)
    setPetName(item.name)
  }

  const Nud = ({ label, value, onChange, min = 0, max = 99999 }: {
    label: string; value: number; onChange: (v: number) => void; min?: number; max?: number
  }) => (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
      <span style={{ width: 110, color: 'var(--text-muted)', fontSize: 13, textAlign: 'right', flexShrink: 0 }}>
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

  const GrowthNud = ({ label, value, onChange, color = 'var(--text-secondary)' }: {
    label: string; value: number; onChange: (v: number) => void; color?: string
  }) => (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
      <span style={{ width: 110, color: 'var(--text-muted)', fontSize: 13, textAlign: 'right', flexShrink: 0 }}>
        {label}
      </span>
      <button onClick={() => onChange(Math.max(0, parseFloat((value - 0.001).toFixed(3))))}
        style={{ background: 'var(--bg-input)', color, padding: '4px 12px', border: '1px solid var(--border)' }}>
        {S.minus}
      </button>
      <input type="number" value={value} step={0.001} min={0} max={99.999}
        onChange={e => {
          const v = parseFloat(e.target.value)
          if (!isNaN(v)) onChange(Math.max(0, Math.min(99.999, parseFloat(v.toFixed(3)))))
        }}
        style={{ width: 100, textAlign: 'center', color }} />
      <button onClick={() => onChange(Math.min(99.999, parseFloat((value + 0.001).toFixed(3))))}
        style={{ background: 'var(--bg-input)', color, padding: '4px 12px', border: '1px solid var(--border)' }}>
        {S.plus}
      </button>
    </div>
  )

  const Card = ({ title, accent, children }: { title: string; accent?: string; children: React.ReactNode }) => (
    <div style={{
      background: 'var(--bg-card)', border: '1px solid var(--border)',
      borderRadius: 10, padding: 20, marginBottom: 16
    }}>
      <h3 style={{ fontSize: 13, fontWeight: 700, color: accent || 'var(--accent-blue)', marginBottom: 14 }}>{title}</h3>
      {children}
    </div>
  )

  const CmdBar = ({ cmd, ckey }: { cmd: string; ckey: string }) => {
    const isMultiLine = cmd.includes('\n')
    return (
      <div style={{ display: 'flex', gap: 8, alignItems: isMultiLine ? 'flex-start' : 'center', marginTop: 12 }}>
        {isMultiLine
          ? <textarea readOnly value={cmd} rows={cmd.split('\n').length} style={{
              flex: 1, background: '#0d1a0d', color: '#6eff8a',
              fontFamily: 'Consolas, monospace', fontSize: 12, fontWeight: 600,
              resize: 'vertical', minHeight: 60
            }} />
          : <input readOnly value={cmd} style={{
              flex: 1, background: '#0d1a0d', color: '#6eff8a',
              fontFamily: 'Consolas, monospace', fontSize: 13, fontWeight: 600
            }} />
        }
        <button onClick={() => copy(cmd, ckey)} style={{
          background: copied === ckey ? 'var(--accent-green)' : 'var(--accent-blue)',
          color: '#fff', padding: '6px 14px', fontSize: 13, flexShrink: 0
        }}>
          {copied === ckey ? S.copied : S.copy}
        </button>
      </div>
    )
  }

  const InfoRow = ({ label, value, color }: { label: string; value: string; color?: string }) => (
    <div style={{ display: 'flex', gap: 8, fontSize: 13, marginBottom: 4 }}>
      <span style={{ color: 'var(--text-muted)', width: 110, textAlign: 'right', flexShrink: 0 }}>{label}</span>
      <span style={{ color: color || 'var(--text-secondary)', fontWeight: 600 }}>{value}</span>
    </div>
  )

  return (
    <div style={{ padding: 28, maxWidth: 700 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>
        🐾 {S.pagePetCmd}
      </h1>

      <Card title="👤 指定玩家帳號（CDKEY）">
        <PlayerAutocomplete
          value={playerQ}
          onChange={setPlayerQ}
          onSelect={onSelectPlayer}
          onSelectMulti={onSelectMultiPlayer}
          placeholder="主帳號 / 角色名 / UID（主帳號可複選全部子帳號）"
        />
        {pickedPlayers.length > 0 && (
          <div style={{ marginTop: 8 }}>
            {pickedPlayers.length === 1
              ? <p style={{ fontSize: 13, color: 'var(--accent-green)' }}>✓ 已選：{charName}（{cdkey}）</p>
              : (
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5 }}>
                  {pickedPlayers.map(p => (
                    <span key={p.account} style={{
                      background: 'rgba(74,158,255,.15)', border: '1px solid rgba(74,158,255,.35)',
                      borderRadius: 20, padding: '2px 8px', fontSize: 12, color: 'var(--accent-blue)'
                    }}>
                      {p.onlineName || p.account}
                    </span>
                  ))}
                </div>
              )
            }
          </div>
        )}
      </Card>

      <Card title="🐾 選擇寵物種類">
        <div style={{ marginBottom: 10 }}>
          <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>
            名稱搜尋（從已上傳的 pets.xlsx 自動偵測）
          </div>
          <ItemAutocomplete
            mode="pet"
            onSelect={onSelectPet}
            placeholder="輸入寵物名稱或編號…"
          />
        </div>
        {petName && (
          <p style={{ fontSize: 13, color: 'var(--accent-green)', marginBottom: 8 }}>
            ✓ 已選：{petName} (#{petId})
          </p>
        )}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 4 }}>
          <span style={{ fontSize: 12, color: 'var(--text-muted)', flexShrink: 0 }}>或手動輸入編號：</span>
          <Nud label="" value={petId} onChange={v => { setPetId(v); setPetName('') }} min={1} />
        </div>
        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 4 }}>
          ⚠ 請先到「發送道具」頁面點擊「上傳 pets.xlsx」載入寵物清單
        </div>
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

      {/* ── 直接輸入（原始）──────────────────────────────── */}
      <Card title={`⚙️ ${S.petAbiTitle}（直接輸入原始參數）`}>
        <p style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 12 }}>
          直接填入 GM 指令所需的原始數值（非面板顯示值）
        </p>
        <Nud label={S.petHp}      value={hp}     onChange={setHp}     min={1} />
        <Nud label={S.petAtk}     value={atk}    onChange={setAtk} />
        <Nud label={S.petDef}     value={def}    onChange={setDef} />
        <Nud label={S.petSpd}     value={spd}    onChange={setSpd} />
        <Nud label={S.petLevel}   value={abiLv}  onChange={setAbiLv}  min={1} max={200} />
        <Nud label={S.petRebirth} value={abiReb} onChange={setAbiReb} min={0} max={20} />
        <CmdBar cmd={abiCmd} ckey="abi" />
        <p style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 8 }}>{S.petAbiNote}</p>
      </Card>

      {/* ── 目標面板數值反推 ──────────────────────────────── */}
      <Card title="🔢 目標面板數值反推指令（輸入 140 等面板顯示值）" accent="#ffd84d">
        <p style={{ fontSize: 12, color: 'rgba(255,216,77,.7)', marginBottom: 12 }}>
          輸入你希望寵物 140 等時「面板顯示」的數值，系統自動套用補償公式換算 GM 指令參數
        </p>
        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 10,
          background: 'rgba(255,216,77,.05)', border: '1px solid rgba(255,216,77,.2)',
          borderRadius: 6, padding: '8px 12px' }}>
          HP ÷ 0.0764 ｜ ATK × 1.08 ｜ DEF × 0.95 ｜ AGI 不變
        </div>
        <Nud label="目標 HP（血量）" value={tgtHp}  onChange={setTgtHp}  min={1} />
        <Nud label="目標 ATK（攻擊）" value={tgtAtk} onChange={setTgtAtk} min={1} />
        <Nud label="目標 DEF（防禦）" value={tgtDef} onChange={setTgtDef} min={1} />
        <Nud label="目標 AGI（敏捷）" value={tgtAgi} onChange={setTgtAgi} min={1} />

        <div style={{
          background: 'rgba(255,216,77,.07)', border: '1px solid rgba(255,216,77,.25)',
          borderRadius: 8, padding: '10px 14px', marginBottom: 12, marginTop: 4
        }}>
          <div style={{ fontSize: 12, color: '#ffd84d', fontWeight: 700, marginBottom: 8 }}>
            💡 換算後的 GM 寫入參數
          </div>
          <InfoRow label="Input HP =" value={convHp.toLocaleString()} color="#6eff8a" />
          <InfoRow label="Input ATK =" value={convAtk.toString()} color="#ffb93c" />
          <InfoRow label="Input DEF =" value={convDef.toString()} color="#64b9ff" />
          <InfoRow label="Input AGI =" value={convAgi.toString()} color="#b982ff" />
        </div>

        <CmdBar cmd={abiCmd2} ckey="abi2" />

        <div style={{
          background: 'rgba(255,80,80,.07)', border: '1px solid rgba(255,80,80,.25)',
          borderRadius: 8, padding: '10px 14px', marginTop: 12
        }}>
          <div style={{ fontSize: 12, color: '#ff8080', fontWeight: 700, marginBottom: 8 }}>
            📊 預測成長率（概算，基於 1 等平均初始值：攻 19、防 12、敏 12）
          </div>
          <InfoRow label="預測攻擊成長 =" value={predAtk.toFixed(3)} color="#ffb93c" />
          <InfoRow label="預測防禦成長 =" value={predDef.toFixed(3)} color="#64b9ff" />
          <InfoRow label="預測敏捷成長 =" value={predAgiV.toFixed(3)} color="#b982ff" />
          <div style={{ height: 1, background: 'rgba(255,80,80,.2)', margin: '8px 0' }} />
          <InfoRow label="預測總成長 =" value={predTotal.toFixed(3)} color="#ff8080" />
        </div>
      </Card>

      {/* ── 精準三圍反推（兩段式）────────────────────────── */}
      <Card title="✅ 精準三圍反推指令（直接輸入各成長率，兩段式計算）" accent="#80ff80">
        <p style={{ fontSize: 12, color: 'rgba(128,255,128,.7)', marginBottom: 12 }}>
          直接輸入三圍成長率與目標血量 → 步驟一：還原 140 等面板 → 步驟二：套用補償公式 → 生成 GM 指令
        </p>
        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 10,
          background: 'rgba(128,255,128,.05)', border: '1px solid rgba(128,255,128,.2)',
          borderRadius: 6, padding: '8px 12px' }}>
          步驟1：Target = round(成長 × 139 + 初值)　步驟2：Input = round(Target × 補償係數)
        </div>

        <Nud label="最終血量 HP" value={grHp} onChange={setGrHp} min={1} />

        <GrowthNud label="預期攻擊成長" value={grAtk} onChange={setGrAtk} color="#ffb93c" />
        <GrowthNud label="預期防禦成長" value={grDef} onChange={setGrDef} color="#64b9ff" />
        <GrowthNud label="預期敏捷成長" value={grAgi} onChange={setGrAgi} color="#b982ff" />

        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 14, marginTop: 2 }}>
          <span style={{ width: 110, color: 'var(--text-muted)', fontSize: 13, textAlign: 'right', flexShrink: 0 }}>
            預期總成長
          </span>
          <span style={{
            fontSize: 15, fontWeight: 700,
            color: '#ffd84d',
            background: 'rgba(255,216,77,.1)', border: '1px solid rgba(255,216,77,.3)',
            borderRadius: 6, padding: '4px 14px'
          }}>
            {grTotal.toFixed(3)}
          </span>
          <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>（ATK + DEF + AGI 自動加總，唯讀）</span>
        </div>

        <div style={{
          background: 'rgba(128,255,128,.07)', border: '1px solid rgba(128,255,128,.25)',
          borderRadius: 8, padding: '10px 14px', marginBottom: 12
        }}>
          <div style={{ fontSize: 12, color: '#80ff80', fontWeight: 700, marginBottom: 8 }}>
            🔢 步驟一推導出的 140 等目標面板
          </div>
          <InfoRow label="Target ATK =" value={grTgtAtk.toString()} color="#ffb93c" />
          <InfoRow label="Target DEF =" value={grTgtDef.toString()} color="#64b9ff" />
          <InfoRow label="Target AGI =" value={grTgtAgi.toString()} color="#b982ff" />
        </div>

        <CmdBar cmd={abiCmd3} ckey="abi3" />

        <p style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 8 }}>
          ※ 此方法為兩段式精準計算，直接輸入目標成長率，完全避免比例分配誤差
        </p>
      </Card>
    </div>
  )
}
