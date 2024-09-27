//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//

const child_process = require('child_process');
const os = require('os');
const fs = require('fs');
const glob = require('glob');
const moment = require('moment');
const path = require('path');
const util = require('util');
const yargs = require('yargs');

fs.readdir = util.promisify(fs.readdir);
fs.copyFile = util.promisify(fs.copyFile);
fs.rename = util.promisify(fs.rename);
fs.readFile = util.promisify(fs.readFile);
fs.writeFile = util.promisify(fs.writeFile);
fs.mkdir = util.promisify(fs.mkdir);
fs.unlink = util.promisify(fs.unlink);
yargs.version(false);
const buildGroup = 'Build Options:';
const testGroup = 'Test Options:';


yargs.option('filter', { desc: 'Filter test cases', string: true, group: testGroup });

const srcDir = path.join(__dirname, 'src');
const binDir = path.join(__dirname, 'out', 'bin');
const libDir = path.join(__dirname, 'out', 'lib');
const packageDir = path.join(__dirname, 'out', 'pkg');
const packageJsonFile = path.join(__dirname, 'package.json');
const testResultsDir = path.join(__dirname, 'out', 'testresults');

function getPackageFileName(packageJson, buildVersion) {
    // '@scope/' gets converted to a 'scope-' prefix of the package filename.
    return `${packageJson.name.replace('@', '').replace('/', '-')}-${buildVersion}.tgz`;
}

yargs.command('build', 'Build TypeScript code', async () => {
    await forkCommand('build-ts');
});

yargs.command('pack', 'Build TypeScript packages', async () => {
    await forkCommand('pack-ts');
});

yargs.command('test', 'Test TypeScript code', async () => {
    await forkCommand('test-ts');
});

yargs.command('build-ts', 'Build TypeScript code', async (yargs) => {
    const tsPackageNames = ['contracts', 'management', 'connections'];

    for (let packageName of tsPackageNames) {
        await linkLib('@microsoft/dev-tunnels-' + packageName, packageName);
    }

    await executeCommand(__dirname, `npm run --silent compile`);
    await executeCommand(__dirname, `npm run --silent eslint`);

    const buildVersion = await getBuildVersion();
    const rootPackageJson = JSON.parse(await fs.readFile(path.join(__dirname, 'package.json')));

    // Update the package.json and README for each built package.
    for (let packageName of tsPackageNames) {
        const sourceDir = path.join(srcDir, packageName);
        const targetDir = path.join(libDir, packageName);
        const builtPackageJsonFile = path.join(targetDir, 'package.json');
        const packageJson = JSON.parse(await fs.readFile(builtPackageJsonFile));

        packageJson.version = buildVersion;
        packageJson.author = rootPackageJson.author;
        packageJson.scripts = undefined;
        packageJson.main = './index.js';

        // Force the dependencies on other packages in this project to be the same build version.
        for (let packageName of Object.keys(packageJson.dependencies)) {
            if (packageName.startsWith(rootPackageJson.name + '-')) {
                packageJson.dependencies[packageName] = buildVersion;
            }
        }

        await fs.writeFile(builtPackageJsonFile, JSON.stringify(packageJson, null, '\t'));

        await fs.copyFile(path.join(sourceDir, 'README.md'), path.join(targetDir, 'README.md'));
    }
});

yargs.command('pack-ts', 'Build TypeScript npm packages', async (yargs) => {
    const rootPackageJson = JSON.parse(await fs.readFile(path.join(__dirname, 'package.json')));
    const buildVersion = await getBuildVersion();

    await mkdirp(packageDir);
    let packageFiles = [];

    for (let packageName of ['contracts', 'management', 'connections']) {
        const sourceDir = path.join(srcDir, packageName);
        const targetDir = path.join(libDir, packageName);

        const packageJson = JSON.parse(await fs.readFile(path.join(sourceDir, 'package.json')));
        packageJson.author = rootPackageJson.author;
        packageJson.version = buildVersion;
        packageJson.scripts = undefined;
        packageJson.main = './index.js';
        await fs.writeFile(
            path.join(targetDir, 'package.json'),
            JSON.stringify(packageJson, null, '\t'),
        );

        await fs.copyFile(path.join(sourceDir, 'README.md'), path.join(targetDir, 'README.md'));

        await executeCommand(targetDir, `npm pack`);

        const prefixedPackageFileName = getPackageFileName(packageJson, buildVersion);
        const packageFileName = prefixedPackageFileName.replace(/\w+-/, '');
        await fs.rename(
            path.join(targetDir, prefixedPackageFileName),
            path.join(packageDir, packageFileName),
        );
        packageFiles.push(packageFileName);
    }

    console.log(`Created packages [${packageFiles.join(', ')}] at ${packageDir}`);
});

yargs.command('publish-ts', 'Publish TypeScrypt npm packages', async (yargs) => {
    const buildVersion = await getBuildVersion();

    let fileName = `dev-tunnels-contracts-${buildVersion}.tgz`;
    let packageFilePath = path.join(packageDir, fileName);
    let publishCommand = `npm publish "${packageFilePath}"`;
    await executeCommand(__dirname, publishCommand);

    fileName = `dev-tunnels-management-${buildVersion}.tgz`;
    packageFilePath = path.join(packageDir, fileName);
    publishCommand = `npm publish "${packageFilePath}"`;
    await executeCommand(__dirname, publishCommand);

    fileName = `dev-tunnels-connections-${buildVersion}.tgz`;
    packageFilePath = path.join(packageDir, fileName);
    publishCommand = `npm publish "${packageFilePath}"`;
    await executeCommand(__dirname, publishCommand);
});

yargs.command('test-ts', 'Run TypeScript tests', async (yargs) => {
    await mkdirp(testResultsDir);

    const testResultsFile = path.join(
        testResultsDir,
        `TUNNELS-TS_${moment().format('YYYY-MM-DD_HH-mm-ss-SSS')}.xml`,
    );
    const reporterConfig = {
        reporterEnabled: 'spec, mocha-junit-reporter',
        mochaJunitReporterReporterOptions: {
            mochaFile: testResultsFile,
        },
    };
    const reporterConfigFile = path.join(testResultsDir, 'mocha-multi-reporters.config');
    await fs.writeFile(reporterConfigFile, JSON.stringify(reporterConfig));

    let command =
        'npm run --silent mocha -- --reporter mocha-multi-reporters ' +
        `--reporter-options configFile="${reporterConfigFile}"`;
    if (yargs.argv.filter) {
        command += ` --grep /${yargs.argv.filter}/i`;
    }

    try {
        await executeCommand(__dirname, command);
    } finally {
        await fs.unlink(reporterConfigFile);
    }
});

function forkCommand(command) {
    const args = [command, ...process.argv.slice(3)];
    return new Promise((resolve) => {
        const options = { stdio: 'inherit', shell: true };
        const p = child_process.fork(process.argv[1], args, options);
        p.on('close', (code) => {
            if (code) process.exit(code);
            resolve();
        });
    });
}

function executeCommand(cwd, command, args) {
    if (!args) {
        const parts = command.split(' ');
        command = parts[0];
        args = parts.slice(1);
    }
    console.log(`${command} ${args.join(' ')}`);
    return new Promise((resolve, reject) => {
        const options = { cwd: cwd, stdio: 'inherit', shell: true };
        const p = child_process.spawn(command, args, options, (err) => {
            if (err) {
                err.showStack = false;
                reject(err);
            }
            resolve();
        });
        p.on('close', (code) => {
            if (code) reject(`Command exited with code ${code}: ${command}`);
            resolve();
        });
    });
}

async function mkdirp(dir) {
    try {
        await fs.mkdir(dir, { recursive: true });
    } catch (e) {
        if (e.code !== 'EEXIST') throw e;
    }
}

async function getBuildVersion() {
    const nbgv = require('nerdbank-gitversioning');
    const buildVersion = await nbgv.getVersion();
    return buildVersion.semVer2;
}

async function linkLib(packageName, dirName) {
    const libModuleFile = path.join(libDir, 'node_modules', packageName + '.js');
    await mkdirp(path.dirname(libModuleFile));
    await fs.writeFile(
        libModuleFile,
        '// Enable referencing this lib by package name instead of by relative path.\n' +
            `module.exports = require('../../${dirName}');\n`,
    );
}

yargs.parseAsync().catch(console.error);
