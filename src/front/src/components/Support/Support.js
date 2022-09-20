// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React from "react"

const Support = () => {
    reutrn(
        <section id="support" className="contact-area">
            <div className="container">
                <div className="row justify-content-center">
                    <div className="col-lg-6 col-md-10">
                        <div className="section-title text-center pb-30">
                            <h3 className="title">Support</h3>
                            <p className="text">Having a hard time to get in or to use the system? You can always ask for help through one of the channels below.</p>
                        </div>
                    </div>
                </div>

                <div className="contact-info pt-30">
                    <div className="row">
                        <div className="col-lg-4 col-md-6">
                            <div className="single-contact-info contact-color-1 mt-30 d-flex ">
                                <div className="contact-info-icon">
                                    <i className="lni lni-map-marker"></i>
                                </div>
                                <div className="contact-info-content media-body">
                                    <p className="text"> Elizabeth St, Melbourne<br />1202 Australia.</p>
                                </div>
                            </div>
                        </div>
                        <div className="col-lg-4 col-md-6">
                            <div className="single-contact-info contact-color-2 mt-30 d-flex ">
                                <div className="contact-info-icon">
                                    <i className="lni lni-envelope"></i>
                                </div>
                                <div className="contact-info-content media-body">
                                    <p className="text">hello@ayroui.com</p>
                                    <p className="text">support@uideck.com</p>
                                </div>
                            </div>
                        </div>
                        <div className="col-lg-4 col-md-6">
                            <div className="single-contact-info contact-color-3 mt-30 d-flex ">
                                <div className="contact-info-icon">
                                    <i className="lni lni-phone"></i>
                                </div>
                                <div className="contact-info-content media-body">
                                    <p className="text">+333 789-321-654</p>
                                    <p className="text">+333 985-458-609</p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                <div className="row">
                    <div className="col-lg-12">
                        <div className="contact-wrapper form-style-two pt-115">
                            <h4 className="contact-title pb-10"><i className="lni lni-envelope"></i> Leave <span>A Message.</span></h4>

                            <form id="contact-form" action="assets/contact.php" method="post">
                                <div className="row">
                                    <div className="col-md-6">
                                        <div className="form-input mt-25">
                                            <label>Name</label>
                                            <div className="input-items default">
                                                <input name="name" type="text" placeholder="Name" />
                                                    <i className="lni lni-user"></i>
                                            </div>
                                        </div>
                                    </div>
                                    <div className="col-md-6">
                                        <div className="form-input mt-25">
                                            <label>Email</label>
                                            <div className="input-items default">
                                                <input type="email" name="email" placeholder="Email" />
                                                    <i className="lni lni-envelope"></i>
                                            </div>
                                        </div>
                                    </div>
                                    <div className="col-md-12">
                                        <div className="form-input mt-25">
                                            <label>Massage</label>
                                            <div className="input-items default">
                                                <textarea name="massage" placeholder="Massage"></textarea>
                                                <i className="lni lni-pencil-alt"></i>
                                            </div>
                                        </div>
                                    </div>
                                    <p className="form-message"></p>
                                    <div className="col-md-12">
                                        <div className="form-input light-rounded-buttons mt-30">
                                            <button className="main-btn light-rounded-two">Send Message</button>
                                        </div>
                                    </div>
                                </div>
                            </form>
                        </div>
                    </div>
                </div>
            </div>
        </section>
    )
}