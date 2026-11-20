// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

'use strict';

const fs = require('fs');
const path = require('path');
const Mocha = require('mocha');

const { Spec } = Mocha.reporters;
const {
    EVENT_TEST_PASS,
    EVENT_TEST_FAIL,
    EVENT_TEST_PENDING,
    EVENT_RUN_END,
} = Mocha.Runner.constants;

// Strip characters disallowed in XML 1.0 (e.g. ANSI/control characters in test output).
// eslint-disable-next-line no-control-regex
const INVALID_XML_CHARS = /[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/g;

function escapeXml(value) {
    return String(value ?? '')
        .replace(INVALID_XML_CHARS, '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&apos;');
}

/**
 * Mocha 12-compatible reporter that writes Spec output to stdout and JUnit XML to a file.
 * Replaces mocha-multi-reporters + mocha-junit-reporter, both of which invoke Mocha's Base
 * reporter without `new` and fail on Mocha 12 where Base is an ES6 class.
 */
class SpecJUnitReporter extends Spec {
    constructor(runner, options) {
        super(runner, options);

        const reporterOptions = (options && options.reporterOptions) || {};
        const mochaFile = reporterOptions.mochaFile;
        const suites = new Map();
        const runStart = new Date();

        const recordTest = (test, status, err) => {
            const suiteName = (test.parent && test.parent.fullTitle()) || 'Root Suite';
            let suite = suites.get(suiteName);
            if (!suite) {
                suite = { name: suiteName, tests: [], failures: 0, skipped: 0, time: 0 };
                suites.set(suiteName, suite);
            }

            const durationSec = (test.duration || 0) / 1000;
            suite.time += durationSec;
            if (status === 'fail') {
                suite.failures += 1;
            } else if (status === 'pending') {
                suite.skipped += 1;
            }

            suite.tests.push({
                name: test.title,
                classname: suiteName,
                time: durationSec.toFixed(4),
                status,
                err,
            });
        };

        runner.on(EVENT_TEST_PASS, (test) => recordTest(test, 'pass'));
        runner.on(EVENT_TEST_FAIL, (test, err) => recordTest(test, 'fail', err));
        runner.on(EVENT_TEST_PENDING, (test) => recordTest(test, 'pending'));

        runner.once(EVENT_RUN_END, () => {
            if (!mochaFile) {
                return;
            }

            let totalTests = 0;
            let totalFailures = 0;
            let totalTime = 0;
            for (const suite of suites.values()) {
                totalTests += suite.tests.length;
                totalFailures += suite.failures;
                totalTime += suite.time;
            }

            const lines = [
                '<?xml version="1.0" encoding="UTF-8"?>',
                `<testsuites name="Mocha Tests" time="${totalTime.toFixed(4)}" tests="${totalTests}" failures="${totalFailures}">`,
            ];

            const timestamp = runStart.toISOString().slice(0, -5);
            for (const suite of suites.values()) {
                lines.push(
                    `  <testsuite name="${escapeXml(suite.name)}" timestamp="${timestamp}" tests="${suite.tests.length}" time="${suite.time.toFixed(4)}" failures="${suite.failures}" skipped="${suite.skipped}">`,
                );
                for (const t of suite.tests) {
                    const attrs = `name="${escapeXml(t.name)}" time="${t.time}" classname="${escapeXml(t.classname)}"`;
                    if (t.status === 'pass') {
                        lines.push(`    <testcase ${attrs}/>`);
                    } else if (t.status === 'pending') {
                        lines.push(`    <testcase ${attrs}>`);
                        lines.push('      <skipped/>');
                        lines.push('    </testcase>');
                    } else {
                        const err = t.err || {};
                        const msg = err.message || String(err);
                        const type = err.name || 'Error';
                        const stack = err.stack || msg;
                        lines.push(`    <testcase ${attrs}>`);
                        lines.push(
                            `      <failure message="${escapeXml(msg)}" type="${escapeXml(type)}">${escapeXml(stack)}</failure>`,
                        );
                        lines.push('    </testcase>');
                    }
                }
                lines.push('  </testsuite>');
            }

            lines.push('</testsuites>', '');

            fs.mkdirSync(path.dirname(mochaFile), { recursive: true });
            fs.writeFileSync(mochaFile, lines.join('\n'), 'utf8');
        });
    }
}

module.exports = SpecJUnitReporter;
