<script>
	import db from "$lib/connections";
	import { onMount } from "svelte";

	let pageContainer;

	const SETTINGS_KEY = "tv_page_html";
	const RELOAD_INTERVAL_MS = 30 * 60 * 1000;

	let cleanupDynamicPage = () => {};

	function extractScripts(html) {
		const template = document.createElement("template");
		template.innerHTML = html;

		const scripts = Array.from(template.content.querySelectorAll("script"));
		const scriptCode = scripts.map((script) => script.textContent ?? "");

		for (const script of scripts) {
			script.remove();
		}

		return {
			template,
			scriptCode
		};
	}

	function getVariableNames(code) {
		const names = new Set();

		const declarationRegex = /\b(?:let|const|var)\s+([a-zA-Z_$][\w$]*)/g;

		let match;
		while ((match = declarationRegex.exec(code)) !== null) {
			names.add(match[1]);
		}

		return names;
	}

	function transformDeclarations(code) {
		return code.replace(
			/\b(?:let|const|var)\s+([a-zA-Z_$][\w$]*)(\s*=\s*[^;]+)?;/g,
			(_, name, initializer) => {
				return initializer ? `${name}${initializer};` : `${name} = undefined;`;
			}
		);
	}

	function evaluateExpression(expression, state, context) {
		try {
			const fn = new Function(
				"$state",
				"$context",
				`
					with ($context) {
						with ($state) {
							return (${expression});
						}
					}
				`
			);

			return fn(state, context);
		} catch (error) {
			console.error(`Failed to evaluate expression: ${expression}`, error);
			return "";
		}
	}

	function applyBindings(root, state, context) {
		const walker = document.createTreeWalker(
			root,
			NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT
		);

		let node;

		while ((node = walker.nextNode())) {
			if (node.nodeType === Node.TEXT_NODE) {
				const parentName = node.parentElement?.tagName?.toLowerCase();

				if (parentName === "style" || parentName === "script") {
					continue;
				}

				const originalText = node.textContent ?? "";

				if (originalText.includes("{")) {
					node.textContent = originalText.replace(/\{([^{}]+)\}/g, (_, expression) => {
						const value = evaluateExpression(expression.trim(), state, context);
						return value ?? "";
					});
				}
			}

			if (node.nodeType === Node.ELEMENT_NODE) {
				for (const attribute of Array.from(node.attributes)) {
					const originalValue = attribute.value;

					if (!originalValue.includes("{")) {
						continue;
					}

					const newValue = originalValue.replace(/\{([^{}]+)\}/g, (_, expression) => {
						const value = evaluateExpression(expression.trim(), state, context);
						return value ?? "";
					});

					node.setAttribute(attribute.name, newValue);
				}
			}
		}
	}

	function renderDynamicPage(source) {
		cleanupDynamicPage();

		const { template, scriptCode } = extractScripts(source);

		let isAlive = true;
		let renderQueued = false;

		const cleanupFunctions = [];

		const rawState = {};
		const allScriptCode = scriptCode.join("\n");

		for (const name of getVariableNames(allScriptCode)) {
			rawState[name] = undefined;
		}

		const state = new Proxy(rawState, {
			has(target, key) {
				return key in target;
			},

			get(target, key) {
				return target[key];
			},

			set(target, key, value) {
				if (target[key] === value) {
					return true;
				}

				target[key] = value;
				queueRender();

				return true;
			}
		});

		function queueRender() {
			if (!isAlive || renderQueued) {
				return;
			}

			renderQueued = true;

			queueMicrotask(() => {
				renderQueued = false;
				renderNow();
			});
		}

		function renderNow() {
			if (!pageContainer || !isAlive) {
				return;
			}

			const fragment = template.content.cloneNode(true);

			applyBindings(fragment, state, context);

			pageContainer.replaceChildren(fragment);
		}

		function dynamicOnMount(callback) {
			const cleanup = callback();

			if (typeof cleanup === "function") {
				cleanupFunctions.push(cleanup);
			}
		}

		const context = {
			db,
			onMount: dynamicOnMount,

			window,
			document,
			console,

			fetch,

			setTimeout: window.setTimeout.bind(window),
			clearTimeout: window.clearTimeout.bind(window),
			setInterval: window.setInterval.bind(window),
			clearInterval: window.clearInterval.bind(window)
		};

		async function runScript(code) {
			const transformedCode = transformDeclarations(code);

			const fn = new Function(
				"$state",
				"$context",
				`
					with ($context) {
						with ($state) {
							${transformedCode}
						}
					}
				`
			);

			return await fn(state, context);
		}

		renderNow();

		for (const code of scriptCode) {
			runScript(code).catch((error) => {
				console.error("Dynamic page script failed:", error);
			});
		}

		cleanupDynamicPage = () => {
			isAlive = false;

			for (const cleanup of cleanupFunctions) {
				try {
					cleanup();
				} catch (error) {
					console.error("Dynamic page cleanup failed:", error);
				}
			}

			cleanupFunctions.length = 0;
			pageContainer?.replaceChildren();
		};
	}

	onMount(async () => {
		const record = await db
			.collection("settings")
			.getFirstListItem(`key="${SETTINGS_KEY}"`);

		renderDynamicPage(record.value ?? "");

		const unsubscribe = await db.collection("settings").subscribe(record.id, (event) => {
			if (event.action === "update" || event.action === "delete") {
				window.location.reload();
			}
		});

		const reloadInterval = setInterval(() => {
			window.location.reload();
		}, RELOAD_INTERVAL_MS);

		return () => {
			clearInterval(reloadInterval);
			unsubscribe();
			cleanupDynamicPage();
		};
	});
</script>

<div bind:this={pageContainer} class="w-screen h-screen"></div>