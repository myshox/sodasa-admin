import axios from 'axios'

// 本機開發用 /api（Vite 會 proxy 到 5050）；部署時可設 VITE_API_URL 指向後端網址
const baseURL = import.meta.env.VITE_API_URL ?? '/api'
const api = axios.create({ baseURL })

api.interceptors.request.use(cfg => {
  const token = localStorage.getItem('gm_token')
  if (token) cfg.headers.Authorization = `Bearer ${token}`
  return cfg
})

api.interceptors.response.use(r => r, err => {
  if (err.response?.status === 401) {
    localStorage.removeItem('gm_token')
    window.location.href = '/login'
  }
  return Promise.reject(err)
})

export default api

export interface PlayerRow {
  account: string; onlineName: string; isOnline: boolean
  serverId: number; regTime: string; loginTime: string
  ip: string; isBanned: boolean; gold: number; crystal: number; petCount: number
  payTotal: number; masterName: string; vipLevel: number
}
export interface PlayerDetail extends PlayerRow {
  regIP: string; uid: string; mac: string; isMuted: boolean
  banEndTime: string; totalMails: number; unreadMails: number
  payPoint: number; rmbPoint: number; groupId: number; neiCe: number
  paydataPoint: number; paydataTotal: number; totalCheck: number
}
export interface MailHistoryItem {
  mailId: number; itemId: number; itemName: string
  quantity: number; sendTime: string; isRead: boolean
}
export interface DashboardStats {
  totalPlayers: number; onlinePlayers: number; bannedPlayers: number
  newToday: number; totalGold: number; totalCrystal: number
}
