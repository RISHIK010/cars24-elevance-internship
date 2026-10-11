"use client";

import React, { createContext, useContext, useEffect, useState } from "react";
import * as api from "@/lib/userapi";
type User = {
  id: string;
  email: string;
  fullName: string;
  phone: string;
};

type AuthContextType = {
  user: User | null;
  loading: boolean;
  signIn: (email: string, password: string) => Promise<void>;
  signUp: (
    email: string,
    password: string,
    userData: Partial<User> & { referralCode?: string }
  ) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  useEffect(() => {
    let cancelled = false;
    const storedUser = localStorage.getItem("user");

    if (!storedUser) {
      setLoading(false);
      return;
    }

    const parsed = (() => {
      try {
        return JSON.parse(storedUser) as User;
      } catch {
        return null;
      }
    })();

    if (!parsed || !parsed.id) {
      localStorage.removeItem("user");
      setLoading(false);
      return;
    }

    // Re-validate the stored identity against the backend before exposing it.
    // A stale user id (e.g. one left over from an earlier database reset or an
    // old account) must never be used to register notification devices or to
    // dispatch appointment notifications under the wrong user.
    api
      .getUserById(parsed.id)
      .then((fresh) => {
        if (cancelled) return;
        if (!fresh) {
          // The stored account no longer exists. Force a fresh sign-in so the
          // current, correct user id is used for notifications and dispatch.
          setUser(null);
          localStorage.removeItem("user");
          return;
        }
        const refreshed: User = {
          id: fresh.id,
          email: fresh.email ?? parsed.email,
          fullName: fresh.fullName ?? parsed.fullName,
          phone: fresh.phone ?? parsed.phone,
        };
        setUser(refreshed);
        localStorage.setItem("user", JSON.stringify(refreshed));
      })
      .catch(() => {
        // Backend unreachable or a transient error: fall back to the stored
        // identity instead of logging the user out on a temporary failure.
        if (!cancelled) setUser(parsed);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const signIn = async (email: string, password: string) => {
    setLoading(true);
    try {
      const userData = await api.login(email, password);
      setUser(userData.user);
      localStorage.setItem("user", JSON.stringify(userData.user));
    } catch (error) {
      console.error("Login failed:", error);
      throw error;
    } finally {
      setLoading(false);
    }
  };
  const signUp = async (
    email: string,
    password: string,
    userData: Partial<any>
  ) => {
    setLoading(true);
    try {
      const newUser = await api.signup(email, password, {
        fullName: userData.fullName,
        phone: userData.phone,
        referralCode: userData.referralCode,
      });
      setUser(newUser.user);
      localStorage.setItem("user", JSON.stringify(newUser.user));
    } catch (error) {
      console.error("Login failed:", error);
      throw error;
    } finally {
      setLoading(false);
    }
  };
  const signOut = async () => {
    setLoading(true);
    try {
      setUser(null);
      localStorage.removeItem("user");
    } catch (error) {
      console.error("Logout failed:", error);
    } finally {
      setLoading(false);
    }
  };
  return (
    <AuthContext.Provider value={{ user, loading, signIn, signUp, signOut }}>
      {children}
    </AuthContext.Provider>
  );
};
export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
};
