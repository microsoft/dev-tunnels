// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as assert from 'assert';
import { suite, test } from '@testdeck/mocha';
import { TunnelAccessTokenProperties } from '@microsoft/dev-tunnels-management';
import { Tunnel } from '@microsoft/dev-tunnels-contracts';

const tunnel: Tunnel = {
    accessTokens: { 'woof': 'dog', 'meow purr': 'cat' },
};

@suite
export class TunnelAccessTokenPropertiesTests {

    @test
    public getTunnelAccessToken() {
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, ['woof']), 'dog');
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, 'woof'), 'dog');
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, ['beep', 'woof']), 'dog');
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, ['woof', 'meow']), 'dog');
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, ['beep', 'meow']), 'cat');
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, ['meow']), 'cat');
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, 'meow'), 'cat');
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, ['purr']), 'cat');
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, 'purr'), 'cat');
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, ['meow', 'woof']), 'cat');
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, ['purr', 'woof']), 'cat');
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(undefined, ['woof']), undefined);
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(undefined, 'woof'), undefined);
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, ['beep']), undefined);
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, 'beep'), undefined);
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, []), undefined);
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, ''), undefined);
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, ' '), undefined);
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, 'meow purr'), undefined);
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(tunnel, undefined), undefined);
        assert.strictEqual(TunnelAccessTokenProperties.getTunnelAccessToken(undefined, undefined), undefined);
    }
}