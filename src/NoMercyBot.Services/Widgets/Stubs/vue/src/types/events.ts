export interface Argument<T = any> {
	EventType: string;
	Data: T;
	Timestamp: Date;
}
