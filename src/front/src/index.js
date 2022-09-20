// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
import React from 'react'
import { render } from 'react-dom'
import './index.css'
import "bootstrap/dist/css/bootstrap.min.css"
import App from './components/App'

render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
  document.getElementById('root')
)
