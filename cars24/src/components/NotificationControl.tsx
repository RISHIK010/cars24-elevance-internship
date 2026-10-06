import { Bell, BellOff, BellRing, Loader2, X } from "lucide-react";
import { useAuth } from "@/context/AuthContext";
import { useRouter } from "next/router";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import {
  disableNotifications,
  enableNotifications,
  getCurrentPermission,
  getDeviceId,
  isFirebaseConfigured,
  isNotificationSupported,
  onForegroundMessage,
} from "@/lib/firebase";
import {
  defaultPreferences,
  getPreferences,
  registerDevice,
  unregisterDevice,
  updatePreferences,
  type NotificationPreferences,
} from "@/lib/notifications";

type Status =
  | "loading"
  | "unsupported"
  | "unconfigured"
  | "denied"
  | "disabled"
  | "enabled"
  | "error";

const ENABLED_KEY = "cars24-notifications-enabled";

const PREFERENCE_LABELS: {
  key: keyof NotificationPreferences;
  label: string;
}[] = [
  { key: "browserPush", label: "Browser Push" },
  { key: "appointmentConfirmations", label: "Appointment Confirmations" },
  { key: "bidUpdates", label: "Bid Updates" },
  { key: "priceDrops", label: "Price Drops" },
  { key: "newMessages", label: "New Messages" },
];

const NotificationControl = () => {
  const { user } = useAuth();
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [status, setStatus] = useState<Status>("loading");
  const [busy, setBusy] = useState(false);
  const [preferences, setPreferences] =
    useState<NotificationPreferences>(defaultPreferences);

  useEffect(() => {
    if (!user) return;
    let cancelled = false;

    async function init() {
      if (!isNotificationSupported()) {
        setStatus("unsupported");
        return;
      }
      if (!isFirebaseConfigured()) {
        setStatus("unconfigured");
        return;
      }
      if (getCurrentPermission() === "denied") {
        setStatus("denied");
        return;
      }

      const enabled = localStorage.getItem(ENABLED_KEY) === "true";
      setStatus(enabled ? "enabled" : "disabled");

      try {
        const stored = await getPreferences(user!.id);
        if (!cancelled && stored) setPreferences(stored);
      } catch {
        // Keep local defaults if preferences cannot be loaded.
      }
    }

    init();
    return () => {
      cancelled = true;
    };
  }, [user]);

  useEffect(() => {
    if (!user || status !== "enabled") return;
    return onForegroundMessage((payload) => {
      const data = payload?.data ?? {};
      const title = data.title || "Cars24";
      const body = data.body || "";
      const actionUrl = data.actionUrl;
      toast(title, {
        description: body,
        action: actionUrl
          ? { label: "View", onClick: () => router.push(actionUrl) }
          : undefined,
      });
    });
  }, [user, status, router]);

  const handleEnable = async () => {
    if (!user) return;
    setBusy(true);
    try {
      const { token, deviceId } = await enableNotifications();
      await registerDevice({
        userId: user.id,
        deviceId,
        registrationToken: token,
        preferences,
      });
      localStorage.setItem(ENABLED_KEY, "true");
      setStatus("enabled");
      toast.success("Notifications enabled");
    } catch {
      if (getCurrentPermission() === "denied") setStatus("denied");
      else if (!isFirebaseConfigured()) setStatus("unconfigured");
      else setStatus("error");
      toast.error("Could not enable notifications");
    } finally {
      setBusy(false);
    }
  };

  const handleDisable = async () => {
    if (!user) return;
    setBusy(true);
    try {
      const deviceId = getDeviceId();
      if (deviceId) await unregisterDevice(user.id, deviceId);
      await disableNotifications();
      localStorage.removeItem(ENABLED_KEY);
      setStatus("disabled");
      toast.success("Notifications disabled");
    } catch {
      toast.error("Could not disable notifications");
    } finally {
      setBusy(false);
    }
  };

  const handlePreferenceChange = async (
    patch: Partial<NotificationPreferences>
  ) => {
    if (!user) return;
    const next = { ...preferences, ...patch };
    setPreferences(next);
    if (status !== "enabled") return;
    try {
      await updatePreferences(user.id, next);
    } catch {
      toast.error("Failed to save preferences");
    }
  };

  if (!user) return null;

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        className="relative inline-flex items-center justify-center rounded-md p-2 text-gray-700 hover:text-blue-600"
        aria-label="Notification settings"
      >
        {status === "enabled" ? (
          <BellRing className="h-5 w-5" />
        ) : (
          <Bell className="h-5 w-5" />
        )}
        {status === "enabled" && (
          <span className="absolute -top-0.5 -right-0.5 h-2.5 w-2.5 rounded-full bg-green-500 ring-2 ring-white" />
        )}
      </button>

      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setOpen(false)} />
          <div className="absolute right-0 z-50 mt-2 w-80 rounded-lg border bg-white p-4 shadow-lg">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold text-gray-900">
                Notifications
              </h3>
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="rounded p-1 text-gray-400 hover:text-gray-600"
                aria-label="Close"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="mt-3">
              {status === "loading" && (
                <div className="flex items-center gap-2 text-sm text-gray-500">
                  <Loader2 className="h-4 w-4 animate-spin" /> Loading…
                </div>
              )}
              {status === "unsupported" && (
                <p className="text-sm text-gray-600">
                  Push notifications aren&apos;t supported in this browser.
                </p>
              )}
              {status === "unconfigured" && (
                <p className="text-sm text-gray-600">
                  Notifications aren&apos;t configured for this environment.
                </p>
              )}
              {status === "denied" && (
                <p className="text-sm text-amber-700">
                  Notifications are blocked. Allow them in your browser settings
                  to receive updates.
                </p>
              )}
              {status === "error" && (
                <p className="text-sm text-red-600">
                  Something went wrong. Please try enabling again.
                </p>
              )}

              {(status === "disabled" ||
                status === "enabled" ||
                status === "error") && (
                <div className="mt-2">
                  {status === "enabled" ? (
                    <button
                      type="button"
                      onClick={handleDisable}
                      disabled={busy}
                      className="inline-flex w-full items-center justify-center gap-2 rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
                    >
                      {busy ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : (
                        <BellOff className="h-4 w-4" />
                      )}
                      Disable Notifications
                    </button>
                  ) : (
                    <button
                      type="button"
                      onClick={handleEnable}
                      disabled={busy}
                      className="inline-flex w-full items-center justify-center gap-2 rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                    >
                      {busy ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : (
                        <Bell className="h-4 w-4" />
                      )}
                      Enable Notifications
                    </button>
                  )}
                </div>
              )}
            </div>

            <div className="mt-4 border-t pt-3">
              <p className="mb-2 text-xs font-medium uppercase tracking-wide text-gray-500">
                Preferences
              </p>
              <div className="space-y-2">
                {PREFERENCE_LABELS.map(({ key, label }) => (
                  <div
                    key={key}
                    className="flex items-center justify-between gap-2"
                  >
                    <span className="text-sm text-gray-700">{label}</span>
                    <button
                      type="button"
                      role="switch"
                      aria-checked={preferences[key]}
                      disabled={status !== "enabled"}
                      onClick={() =>
                        handlePreferenceChange({
                          [key]: !preferences[key],
                        } as Partial<NotificationPreferences>)
                      }
                      className={`relative inline-flex h-5 w-9 items-center rounded-full transition-colors disabled:cursor-not-allowed disabled:opacity-40 ${
                        preferences[key] ? "bg-blue-600" : "bg-gray-300"
                      }`}
                    >
                      <span
                        className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                          preferences[key]
                            ? "translate-x-4"
                            : "translate-x-0.5"
                        }`}
                      />
                    </button>
                  </div>
                ))}
              </div>
              {status !== "enabled" && (
                <p className="mt-2 text-xs text-gray-400">
                  Enable notifications to adjust preferences.
                </p>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
};

export default NotificationControl;
