import axios from 'axios'

const api = axios.create({ baseURL: '/api' })

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
}
export interface PlayerDetail extends PlayerRow {
  regIP: string; uid: string; mac: string; isMuted: boolean
  banEndTime: string; payTotal: number; totalMails: number; unreadMails: number
}
export interface DashboardStats {
  totalPlayers: number; onlinePlayers: number; bannedPlayers: number
  newToday: number; totalGold: number; totalCrystal: number
}
