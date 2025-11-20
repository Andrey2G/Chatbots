const sources = {};

export function startEventStream(key, url, dotNetHelper) {
    stopEventStream(key);
    const source = new EventSource(url);
    sources[key] = source;

    source.onmessage = (event) => {
        dotNetHelper.invokeMethodAsync('HandleStreamEvent', event.data);
    };

    source.addEventListener('done', () => {
        dotNetHelper.invokeMethodAsync('HandleStreamComplete');
        stopEventStream(key);
    });

    source.onerror = () => {
        dotNetHelper.invokeMethodAsync('HandleStreamError', 'Stream disconnected');
        stopEventStream(key);
    };
}

export function stopEventStream(key) {
    const existing = sources[key];
    if (existing) {
        existing.close();
        delete sources[key];
    }
}
