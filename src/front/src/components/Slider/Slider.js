// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React from "react"

const Slider = () => {
    return (
        <section id="home" className="slider_area">
            <div id="carouselThree" className="carousel slide" data-ride="carousel">
                <div className="carousel-inner">
                    <div className="carousel-item active">
                        <div className="container">
                            <div className="row">
                                <div className="col-lg-6">
                                    <div className="slider-content">
                                        <h1 className="title">Storage as Service</h1>
                                        <p className="text">We made it simple to get a private space in <span id="institutionName"></span>'s cloud to store your stuff.</p>
                                        <ul className="slider-btn rounded-buttons">
                                            <li><a className="main-btn rounded-one" href="#">LOG IN</a></li>
                                            <li><a className="main-btn rounded-two page-scroll" href="#howtogainaccess">HOW TO GET ACCESS</a></li>
                                        </ul>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div className="slider-image-box d-none d-lg-flex align-items-end">
                            <div className="slider-image">
                                <img src="assets/images/slider/1.png" alt="Hero" />
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </section>
    )
}

export default Slider
