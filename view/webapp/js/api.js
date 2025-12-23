let jwtToken = null;

export function setToken(token) {
    jwtToken = token;
}

export async function apiFetch(url, options = {}) {
    options.headers = options.headers || {};
    if (jwtToken) {
        options.headers["Authorization"] = `Bearer ${jwtToken}`;
    }

    const res = await fetch(url, options);

    // Try to parse JSON, but fallback if empty
    let data;
    try {
        data = await res.json();
    } catch {
        data = {};
    }

    if (!res.ok) throw new Error(data.message || "API error");
    return data;
}
