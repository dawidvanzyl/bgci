import { expect as baseExpect } from '@playwright/test';

// The default probes of `expect.poll` come after 100, 250 and 500 ms and then every second, so a wait on a job ends
// up to a second after the job. Most job waits of the suite take less than a second. The suite probes every 100 ms.
// A call that gives its own `intervals` keeps them.
const pollIntervals = [100];

const poll: typeof baseExpect.poll = (actual, messageOrOptions) => {
	const options = typeof messageOrOptions === 'string' ? { message: messageOrOptions } : (messageOrOptions ?? {});
	return baseExpect.poll(actual, { intervals: pollIntervals, ...options });
};

/** The Playwright `expect`, with faster default probes for `expect.poll`. Every file of the suite uses this one. */
export const expect: typeof baseExpect = new Proxy(baseExpect, {
	get: (target, property, receiver) => (property === 'poll' ? poll : Reflect.get(target, property, receiver)),
});
