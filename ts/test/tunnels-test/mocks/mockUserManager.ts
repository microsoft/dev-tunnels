// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { UserManager } from '../userManager';
import { authenticationTokenStatus, UserInfo } from '../userInfo';

export class MockUserManager implements UserManager {
    public currentUser?: UserInfo;
    public loginUser: UserInfo = {
        provider: 'mock-provider',
        username: 'mock-user',
        accessToken: 'mock-access-token',
        tokenStatus: authenticationTokenStatus.Valid,
        tokenExpiration: new Date(Date.now() + 3600 * 1000 * 24),
    };

    public getCurrentUser(): Promise<UserInfo> {
        return new Promise<UserInfo>((resolve) => {
            resolve(
                this.currentUser ??
                    ({
                        tokenStatus: authenticationTokenStatus.None,
                    } as UserInfo),
            );
        });
    }

    public login(options: LoginOptions, deviceCodeCallback: Function): Promise<UserInfo> {
        this.currentUser = this.loginUser;
        return new Promise<UserInfo>((resolve) => {
            resolve(this.currentUser!);
        });
    }

    public logout() {
        this.currentUser = {
            tokenStatus: authenticationTokenStatus.None,
        } as UserInfo;

        return this.currentUser;
    }
}

export class LoginOptions {
    public userBrowserAuth?: boolean;
    public useDeviceCodeAuth?: boolean;
    public useIntegratedWindowsAuth?: boolean;
}
