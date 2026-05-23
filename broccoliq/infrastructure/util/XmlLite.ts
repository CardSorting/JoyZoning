/**
 * XmlLite provides a lightweight, high-performance XML-like serialization for agent signaling.
 * It is designed for speed and low overhead in the distributed swarm environment.
 */
export const XmlLite = {
	serialize(
		tag: string,
		attributes: Record<string, unknown>,
		content: unknown,
	): string {
		const attrStr = Object.entries(attributes)
			.map(([k, v]) => ` ${k}="${this.escape(String(v))}"`)
			.join("");

		const body =
			typeof content === "object" && content !== null
				? JSON.stringify(content)
				: this.escape(String(content ?? ""));

		return `<swarm:${tag}${attrStr}>${body}</swarm:${tag}>`;
	},

	parse(xml: string): {
		tag: string;
		attributes: Record<string, string>;
		content: unknown;
	} {
		const match = xml.match(/^<swarm:([^ >]+)([^>]*)>(.*)<\/swarm:\1>$/s);
		if (!match) throw new Error("Invalid XML-Lite format");

		const tag = match[1] as string;
		const attrRaw = match[2] as string;
		const rawContent = match[3] as string;

		const attributes: Record<string, string> = {};
		const attrRegex = /([^ ]+)="([^"]*)"/g;

		let attrMatch = attrRegex.exec(attrRaw);
		while (attrMatch !== null) {
			attributes[attrMatch[1] as string] = this.unescape(
				attrMatch[2] as string,
			);
			attrMatch = attrRegex.exec(attrRaw);
		}

		let content: unknown = rawContent;
		try {
			if (rawContent.startsWith("{") || rawContent.startsWith("[")) {
				content = JSON.parse(rawContent);
			} else {
				content = this.unescape(rawContent);
			}
		} catch {
			content = this.unescape(rawContent);
		}

		return { tag, attributes, content };
	},

	escape(str: string): string {
		return str
			.replace(/&/g, "&amp;")
			.replace(/</g, "&lt;")
			.replace(/>/g, "&gt;")
			.replace(/"/g, "&quot;");
	},

	unescape(str: string): string {
		return str
			.replace(/&quot;/g, '"')
			.replace(/&gt;/g, ">")
			.replace(/&lt;/g, "<")
			.replace(/&amp;/g, "&");
	},
};
