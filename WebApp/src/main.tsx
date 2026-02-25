import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import './index.css'
import LoginPage   from './pages/LoginPage'
import Layout      from './pages/Layout'
import Dashboard   from './pages/Dashboard'
import PlayersPage from './pages/PlayersPage'
import OnlinePage  from './pages/OnlinePage'
import PetCmdPage  from './pages/PetCmdPage'

const PrivateRoute = ({ children }: { children: React.ReactNode }) =>
  localStorage.getItem('gm_token') ? <>{children}</> : <Navigate to="/login" replace />

ReactDOM.createRoot(document.getElementById('root')!).render(
  <BrowserRouter>
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/" element={<PrivateRoute><Layout /></PrivateRoute>}>
        <Route index element={<Dashboard />} />
        <Route path="players" element={<PlayersPage />} />
        <Route path="online"  element={<OnlinePage />} />
        <Route path="petcmd"  element={<PetCmdPage />} />
      </Route>
    </Routes>
  </BrowserRouter>
)
