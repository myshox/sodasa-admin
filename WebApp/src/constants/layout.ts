/**
 * 手機／平板直向與側欄收合斷點（與 CSS @media max-width 對齊）
 * 含三星 Galaxy S 系列等 Android 手機（約 360–430px CSS 寬）
 */
export const MOBILE_BREAKPOINT = 1024

/** 頂欄／無障礙用：目前路由對應的簡短頁面名稱 */
const ROUTE_TITLES: Record<string, string> = {
  '/': '首頁',
  '/players': '玩家管理',
  '/market': '市場查詢',
  '/records': '全服記錄',
  '/analytics': '數據分析',
  '/system': '系統設定',
  '/master': '主帳號查詢',
  '/vip': 'VIP 玩家',
  '/history': '玩家活動歷程',
  '/batchops': '批量工具',
  '/petcmd': '寵物指令',
  '/recycle': '角色回收桶',
  '/sql': 'SQL 查詢',
  '/db-browser': '資料庫瀏覽',
  '/gmadmin': '工具帳號',
  '/online': '線上玩家',
  '/ban': '封號管理',
  '/streetshop': '攤位商店',
  '/itemsearch': '道具搜尋',
  '/recharge': '充值管理',
  '/tradelog': '交易記錄',
  '/goldlog': '金幣日誌',
  '/mail': '郵件記錄',
  '/shopstats': '商城分析',
  '/tradeaudit': '交易稽核',
  '/gmlog': 'GM 操作日誌',
  '/gmperm': 'GM 權限',
  '/backup': '備份還原',
  '/analytics/player': '玩家活躍分析',
  '/analytics/recharge': '儲值趨勢',
  '/dashboard': '統計面板',
  '/batch': '批量工具',
  '/batchgold': '批量金幣',
  '/itemqueue': '道具給予',
  '/send': '道具給予',
  '/speedban': '加速外掛封禁',
  '/server-status': '伺服器狀態',
  '/cost-milestone': '消費里程碑',
  '/guild': '家族管理',
  '/petrank': '練寵排行榜',
}

export function getRouteTitle(pathname: string): string {
  const p = pathname.replace(/\/$/, '') || '/'
  return ROUTE_TITLES[p] ?? 'GM 後台'
}
