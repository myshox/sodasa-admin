import { useState, useEffect } from 'react'

export default function useIsMobile(breakpoint = 768) {
  const [m, setM] = useState(window.innerWidth < breakpoint)
  useEffect(() => {
    const h = () => setM(window.innerWidth < breakpoint)
    window.addEventListener('resize', h)
    return () => window.removeEventListener('resize', h)
  }, [breakpoint])
  return m
}
