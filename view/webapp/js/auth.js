// auth.js
let API_BASE = "http://localhost:5266/api/Auth";

export async function login(email, password) {
    try {
        const res = await fetch(`${API_BASE}/login`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email, password })
        });

        if (!res.ok) {
            return { error: await res.text() };
        }

        const data = await res.json();
        // Standardize the key name here
        localStorage.setItem("jwtToken", data.token); 

        return data; // Returns { token, role }

    } catch (err) {
        return { error: "Server connection failed" };
    }
}

export async function validateToken(requiredRole = null) {
    const token = localStorage.getItem("jwtToken");
    if (!token) return false;

    try {
        const res = await fetch(`${API_BASE}/validate`, {
            headers: { "Authorization": `Bearer ${token}` }
        });

        if (!res.ok) return false;

        const user = await res.json();
        console.log("Validated User:", user);

        if (requiredRole && user.role !== requiredRole) {
            console.warn("Role mismatch");
            return false;
        }

        return true;
    } catch {
        return false;
    }
}

export function logout() {
    localStorage.removeItem("jwtToken");
    window.location.href = "/pages/login.html";
}