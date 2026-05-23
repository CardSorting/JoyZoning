import { XmlLite } from "../util/XmlLite.js";
import { SqliteQueue } from "./SqliteQueue.js";

export interface Signal<T = unknown> {
	type: string;
	payload: T;
	source: string;
	target?: string;
	timestamp: number;
}

export type SignalHandler<T = unknown> = (signal: Signal<T>) => Promise<void>;

/**
 * Signaling provides an XML-Lite messaging layer for autonomous agent coordination.
 * Agents can broadcast state changes or direct requests to other agents in the swarm.
 */
export class Signaling {
	private queue: SqliteQueue<string>;
	private handlers = new Set<SignalHandler<unknown>>();

	constructor(shardId: string = "signals") {
		this.queue = new SqliteQueue<string>({ shardId });
		this.queue.process(async (job) => {
			if (!job.payload || !job.payload.trim().startsWith("<")) {
				// Level 1 Defense: Ignore non-XML payloads silently
				return;
			}
			try {
				const { tag, attributes, content } = XmlLite.parse(job.payload);
				const signal: Signal<unknown> = {
					type: attributes.type || tag,
					source: attributes.source || "unknown",
					target: attributes.target,
					timestamp: Number(attributes.timestamp || Date.now()),
					payload: content,
				};
				for (const handler of this.handlers) {
					await handler(signal);
				}
			} catch (e) {
				console.error(`[Signaling] Failed to parse XML-Lite signal (${job.id}):`, e);
			}
		});
	}

	async broadcast<T>(type: string, payload: T, source: string) {
		const xml = XmlLite.serialize(
			"signal",
			{ type, source, timestamp: Date.now() },
			payload,
		);
		await this.queue.enqueue(xml, { priority: 1 });
	}

	async unicast<T>(target: string, type: string, payload: T, source: string) {
		const xml = XmlLite.serialize(
			"signal",
			{ target, type, source, timestamp: Date.now() },
			payload,
		);
		await this.queue.enqueue(xml, { priority: 2 });
	}

	subscribe(handler: SignalHandler<unknown>) {
		this.handlers.add(handler);
		return () => this.handlers.delete(handler);
	}
}

export const signalSwarm = new Signaling();
