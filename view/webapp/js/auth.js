import { setToken } from './api.js';

let API_BASE = "http://localhost:5266/api/Auth";

export async function login(email, password) {
    try {
        const res = await fetch("http://localhost:5266/api/Auth/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email, password })
        });

        // If response is not OK, return the text (like "Invalid credentials.")
        if (!res.ok) {
            const text = await res.text();
            return text;
        }

        // If OK, parse JSON
        const data = await res.json();
        // Store JWT token
        localStorage.setItem("jwtToken", data.token);
        return data;

    } catch (err) {
        console.error("Login error:", err);
        return "Login failed due to network or server error.";
    }
}

export async function registerUser(email, password, role) {
    try {
        const res = await fetch(`${API_BASE}/register`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email, password, role })
        });

        if (!res.ok) {
            return await res.text(); // validation error text
        }

        // register returns message only
        const data = await res.json();
        return data; // { message: "User registered successfully" }

    } catch (err) {
        console.error("Register error:", err);
        return "Registration failed due to network or server error.";
    }
}

export function logout() {
    localStorage.removeItem("jwtToken");
    setToken(null);
    window.location.href = "/login.html";
}

export function loadToken() {
    const token = localStorage.getItem("jwtToken");
    if (token) setToken(token);
}

export function requireAuth() {
    const token = localStorage.getItem("jwtToken");
    if (!token) {
        window.location.href = "../pages/login.html";
    } else {
        setToken(token);
    }
}
