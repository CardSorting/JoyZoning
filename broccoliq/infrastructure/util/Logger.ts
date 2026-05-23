export enum LogLevel {
	DEBUG = 0,
	INFO = 1,
	WARN = 2,
	ERROR = 3,
}

/**
 * Structured Logger for the Sovereign Swarm.
 * Supports leveled logging, tag-based filtering, and shard-specific metadata.
 */
export class Logger {
	private level: LogLevel = LogLevel.INFO;

	constructor(private context: string = "SWARM") {
		const envLevel = process.env.LOG_LEVEL?.toUpperCase();
		if (envLevel && envLevel in LogLevel) {
			this.level = LogLevel[envLevel as keyof typeof LogLevel];
		}
	}

	debug(message: string, metadata?: Record<string, unknown>) {
		this.log(LogLevel.DEBUG, message, metadata);
	}

	info(message: string, metadata?: Record<string, unknown>) {
		this.log(LogLevel.INFO, message, metadata);
	}

	warn(message: string, metadata?: Record<string, unknown>) {
		this.log(LogLevel.WARN, message, metadata);
	}

	error(message: string, error?: unknown, metadata?: Record<string, unknown>) {
		const errorMsg = error instanceof Error ? error.message : String(error);
		this.log(LogLevel.ERROR, message, { ...metadata, error: errorMsg });
	}

	private log(
		level: LogLevel,
		message: string,
		metadata?: Record<string, unknown>,
	) {
		if (level < this.level) return;

		const timestamp = new Date().toISOString();
		const levelStr = LogLevel[level];

		// Level 3: Zero-Allocation string building where possible
		let metaStr = "";
		if (metadata && Object.keys(metadata).length > 0) {
			try {
				metaStr = ` | ${JSON.stringify(metadata)}`;
			} catch {
				metaStr = " | [Circular or Unserializable Metadata]";
			}
		}

		const output = `[${timestamp}] [${levelStr}] [${this.context}] ${message}${metaStr}`;

		switch (level) {
			case LogLevel.ERROR:
				console.error(output);
				break;
			case LogLevel.WARN:
				console.warn(output);
				break;
			default:
				console.log(output);
		}
	}

	child(context: string): Logger {
		return new Logger(`${this.context}:${context}`);
	}
}

export const logger = new Logger();
