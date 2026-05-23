export class Mutex {
	private queue: (() => void)[] = [];
	private locked = false;

	constructor(public name: string) {}

	async acquire(timeoutMs = 30000): Promise<() => void> {
		if (!this.locked) {
			this.locked = true;
			return () => this.release();
		}

		return new Promise((resolve, reject) => {
			const timeout = setTimeout(() => {
				// Remove the resolver from the queue if it's still there
				const idx = this.queue.indexOf(resolver);
				if (idx >= 0) this.queue.splice(idx, 1);
				reject(new Error(`[Mutex] Timeout acquiring lock: ${this.name} after ${timeoutMs}ms`));
			}, timeoutMs);

			const resolver = () => {
				clearTimeout(timeout);
				resolve(() => this.release());
			};

			this.queue.push(resolver);
		});
	}

	private release() {
		const next = this.queue.shift();
		if (next) {
			// Hand off the lock immediately to the next in line
			// Avoid setting 'locked = false' to prevent new acquire() calls from stealing it
			next();
		} else {
			this.locked = false;
		}
	}
}
