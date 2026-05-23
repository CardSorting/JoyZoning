import type { Schema } from "../Config.js";
import type { WriteOp } from "./types.js";

/**
 * Level 10: Sovereign Shard Container (Absolute Isolation).
 * Manages the memory-first buffering and indexing for a single database shard.
 */
export class ShardState {
	public activeBuffer = new Map<keyof Schema, WriteOp[]>();
	public inFlightBuffer = new Map<keyof Schema, WriteOp[]>();
	public activeSize = 0;
	public inFlightSize = 0;
	
	private static readonly MAX_BUFFER_SIZE = 10000;

	public isOverloaded(): boolean {
		return this.activeSize > ShardState.MAX_BUFFER_SIZE;
	}
	
	/**
	 * Level 7: Memory-First Indexing
	 * status -> id -> op
	 */
	public activeIndex = new Map<keyof Schema, Map<string, Map<string, WriteOp>>>(); 
	public inFlightIndex = new Map<keyof Schema, Map<string, Map<string, WriteOp>>>();
	
	/**
	 * Level 7: Authoritative Multi-Index
	 * id -> op (latest update for this ID in active buffer)
	 */
	public activeIndexById = new Map<keyof Schema, Map<string, WriteOp>>();
	public inFlightIndexById = new Map<keyof Schema, Map<string, WriteOp>>();
	
	/**
	 * Table:col:value authoritative warmup tracking
	 */
	public warmedIndices = new Set<string>();

	public processingLatencies: number[] = [];
	public enqueueLatencies: number[] = [];
	
	constructor(public readonly shardId: string) {}

	public clearActive() {
		this.activeBuffer.clear();
		this.activeIndex.clear();
		this.activeIndexById.clear();
		this.activeSize = 0;
	}

	public clearInFlight() {
		this.inFlightBuffer.clear();
		this.inFlightIndex.clear();
		this.inFlightIndexById.clear();
		this.inFlightSize = 0;
	}

	public swapToInFlight() {
		this.inFlightBuffer = this.activeBuffer;
		this.inFlightIndex = this.activeIndex;
		this.inFlightIndexById = this.activeIndexById;
		this.inFlightSize = this.activeSize;

		this.activeBuffer = new Map();
		this.activeIndex = new Map();
		this.activeIndexById = new Map();
		this.activeSize = 0;
	}

	private isProcessingDirty = true;
	private isEnqueueDirty = true;
	private sortedProcessing: number[] = [];
	private sortedEnqueue: number[] = [];

	public recordLatency(target: "processing" | "enqueue", value: number) {
		const list = target === "processing" ? this.processingLatencies : this.enqueueLatencies;
		list.push(value);
		if (list.length > 5000) list.shift();
		
		if (target === "processing") this.isProcessingDirty = true;
		else this.isEnqueueDirty = true;
	}

	public calculatePercentile(target: "processing" | "enqueue", percentile: number): number {
		const samples = target === "processing" ? this.processingLatencies : this.enqueueLatencies;
		if (samples.length === 0) return 0;
		
		let sorted = target === "processing" ? this.sortedProcessing : this.sortedEnqueue;
		const isDirty = target === "processing" ? this.isProcessingDirty : this.isEnqueueDirty;

		if (isDirty) {
			sorted = [...samples].sort((a, b) => a - b);
			if (target === "processing") {
				this.sortedProcessing = sorted;
				this.isProcessingDirty = false;
			} else {
				this.sortedEnqueue = sorted;
				this.isEnqueueDirty = false;
			}
		}

		const index = Math.ceil((percentile / 100) * sorted.length) - 1;
		return sorted[index] ?? 0;
	}
}
