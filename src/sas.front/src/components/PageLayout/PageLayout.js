import React from "react"
import CssBaseline from '@mui/material/CssBaseline';
import Container from '@mui/material/Container'
import Header from "../Header"
import Footer from "../Footer"
import './PageLayout.css'

/**
 * Renders the navbar component with a sign-in button if a user is not authenticated
 */
export const PageLayout = ({ children, strings }) => {
    return (
        <>
            <CssBaseline />
            <div className='Page'>
                <Header strings={strings} />
                <div className='sectiondivider' />
                <Container className='Content'>
                    {children}
                </Container>
                <div className='sectiondivider' />
                <Footer strings={strings} />
            </div>
        </>
    )
}
