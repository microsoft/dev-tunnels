module.exports = {
	"env": {
		"browser": true,
		"es6": true,
		"node": true
	},
	"extends": [
		'eslint:recommended',
		'plugin:@typescript-eslint/recommended',
		"prettier"
	],
	"parser": "@typescript-eslint/parser",
	"parserOptions": {
		"project": ["src/*/tsconfig.json"],
		"sourceType": "module"
	},
	"plugins": [
		"eslint-plugin-security",
		"eslint-plugin-jsdoc",
		"@typescript-eslint"
	],
	"root": true,
	"rules": {
		"@typescript-eslint/ban-types": [
			"error",
			{
				"types": {
					"BigInt": false,
				}
			}
		],
		"@typescript-eslint/dot-notation": "error",
		"@typescript-eslint/explicit-member-accessibility": [
			"error",
			{
				"accessibility": "explicit"
			}
		],
		"@typescript-eslint/naming-convention": [
			"error",
			{
				"selector": "variable",
				"format": [
					"camelCase",
					"UPPER_CASE"
				],
				"leadingUnderscore": "forbid",
				"trailingUnderscore": "forbid"
			}
		],
		"@typescript-eslint/no-dynamic-delete": "error",
		"@typescript-eslint/no-empty-function": "off",
		"@typescript-eslint/no-explicit-any": "off",
		"@typescript-eslint/no-floating-promises": "error",
		"@typescript-eslint/no-inferrable-types": "off",
		"@typescript-eslint/no-namespace": "off",
		"@typescript-eslint/no-non-null-assertion": "off",
		"@typescript-eslint/no-require-imports": "off",
		"@typescript-eslint/no-shadow": [
			"error",
			{
				"hoist": "all"
			}
		],
		"@typescript-eslint/no-unused-expressions": "error",
		"@typescript-eslint/no-unused-vars": "off",
		"@typescript-eslint/no-use-before-define": "off",
		"@typescript-eslint/no-var-requires": "off",
		"@typescript-eslint/prefer-namespace-keyword": "error",
		"@typescript-eslint/promise-function-async": "off",
		"brace-style": [
			"error",
			"1tbs"
		],
		"curly": "off",
		"default-case": "error",
		"dot-notation": "off",
		"eqeqeq": [
			"error",
			"smart"
		],
		"guard-for-in": "error",
		"id-denylist": "error",
		"id-match": "error",
		"indent": "off",
		"jsdoc/check-alignment": "error",
		"jsdoc/check-indentation": "off",
		"jsdoc/newline-after-description": "off",
		"no-caller": "error",
		"no-cond-assign": "error",
		"no-debugger": "error",
		"no-empty": "off",
		"no-empty-function": "off",
		"no-eval": "error",
		"no-fallthrough": "error",
		"no-inner-declarations": "off",
		"no-multiple-empty-lines": "error",
		"no-new-wrappers": "error",
		"no-redeclare": "error",
		"no-throw-literal": "error",
		"no-underscore-dangle": "error",
		"no-unused-expressions": "off",
		"no-unused-labels": "error",
		"no-unused-vars": "off",
		"no-var": "error",
		"radix": "error",
		"security/detect-non-literal-require": "error",
		"security/detect-possible-timing-attacks": "error"
	}
};
