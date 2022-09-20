// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { useEffect, useState } from 'react';

const useAuthentication = () => {
    const [auth, setAuth] = useState(null);

    useEffect(() => {
        async function getUserInfo() {
            const response = await fetch('/.auth/me');
            const payload = await response.json();
            return payload.clientPrincipal;
        }

        getUserInfo().then(u => {
            setAuth(u)
        }
        )
    }, []);

    return { account: auth, isAuthenticated: !!auth };
}

export default useAuthentication