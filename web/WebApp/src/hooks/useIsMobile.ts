import { useState, useEffect } from 'react'
import { MOBILE_BREAKPOINT } from '../constants/layout'

/** 預設與側欄／批量工具手機切版一致（寬度小於 1024px） */
export default function useIsMobile(breakpoint = MOBILE_BREAKPOINT) {
  const [m, setM] = useState(() => typeof window !== 'undefined' && window.innerWidth < breakpoint)
  useEffect(() => {
    const h = () => setM(window.innerWidth < breakpoint)
    window.addEventListener('resize', h)
    return () => window.removeEventListener('resize', h)
  }, [breakpoint])
  return m
}
