// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

export class UserInfo {
    public aadProvider?: string = 'AAD';
    public githubProvider?: string = 'GitHub';

    public username?: string;
    public provider?: string;
    public accessToken?: string;
    public tokenStatus?: authenticationTokenStatus;
    public tokenExpiration?: Date;
}

export enum authenticationTokenStatus {
    None,
    Valid,
    Expired,
}
