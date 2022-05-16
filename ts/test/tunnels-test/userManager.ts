// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { CommonOptions } from 'child_process';
import { LoginOptions } from './mocks/mockUserManager';
import { UserInfo } from './userInfo';

export interface UserManager {
    getCurrentUser(): Promise<UserInfo>;
    login(options: LoginOptions, deviceCodeCallback: Function): Promise<UserInfo>;
    getCurrentUser(options: any): Promise<UserInfo>;
}
