import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { getMe, type UserResponse } from '../api/users';
import { login as apiLogin, register as apiRegister } from '../api/auth';
import { getToken, setToken, clearToken, onUnauthorized } from '../api/client';

interface AuthContextValue {
  user: UserResponse | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, displayName: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserResponse | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Any 401 from any apiFetch call, at any point in the session — not just during
    // this initial hydration — clears the token (via api/client.ts) and drops the user,
    // which sends ProtectedRoute back to /login.
    const unsubscribe = onUnauthorized(() => setUser(null));

    if (!getToken()) {
      setLoading(false);
      return unsubscribe;
    }
    getMe()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setLoading(false));

    return unsubscribe;
  }, []);

  async function login(email: string, password: string) {
    const res = await apiLogin(email, password);
    setToken(res.token);
    setUser(await getMe());
  }

  async function register(email: string, password: string, displayName: string) {
    const res = await apiRegister(email, password, displayName);
    setToken(res.token);
    setUser(await getMe());
  }

  function logout() {
    clearToken();
    setUser(null);
  }

  return <AuthContext.Provider value={{ user, loading, login, register, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider');
  return ctx;
}
