import React from 'react'
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import useAuthentication from '../../hooks/useAuthentication'

import PageLayout from '../PageLayout'
import LandingPage from '../LandingPage'
import FileSystemsPage from '../FileSystemsPage'
import strings from '../../config/strings.en-us.js'

function App() {
  const { isAuthenticated } = useAuthentication()

  const content = isAuthenticated ? (
    <BrowserRouter>
      <Routes>
        <Route path='/' element={<FileSystemsPage strings={strings} />} />
      </Routes>
    </BrowserRouter>
  ) : (
    <LandingPage strings={strings} />
  )

  return (
    <PageLayout strings={strings}>
      {content}
    </PageLayout>
  )
}

export default App