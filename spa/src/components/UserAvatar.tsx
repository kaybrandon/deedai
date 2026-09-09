import { useEffect, useState } from "react";
import { endpoints, sessionToken } from "../api";

export function initialsFor(name: string | null | undefined, email?: string | null) {
  const source = (name ?? "").trim() || (email ?? "").trim();
  if (!source) {
    return "?";
  }

  const parts = source.split(/[\s@._-]+/).filter(Boolean);
  const letters = (parts[0]?.[0] ?? "") + (parts.length > 1 ? parts[1][0] : "");
  return letters.toUpperCase() || source[0].toUpperCase();
}

export default function UserAvatar({
  userId,
  name,
  email,
  hasPhoto,
  revision = 0,
  size = "md"
}: {
  userId?: string;
  name?: string | null;
  email?: string | null;
  hasPhoto?: boolean;
  revision?: number;
  size?: "sm" | "md";
}) {
  const [src, setSrc] = useState<string | null>(null);

  useEffect(() => {
    if (!userId || !hasPhoto) {
      setSrc(null);
      return;
    }

    const token = sessionToken();
    const controller = new AbortController();
    let objectUrl: string | null = null;
    fetch(`${endpoints.userPhotoUrl(userId)}?v=${revision}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      signal: controller.signal
    })
      .then((response) => (response.ok ? response.blob() : null))
      .then((blob) => {
        if (!blob) {
          return;
        }
        objectUrl = URL.createObjectURL(blob);
        setSrc(objectUrl);
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setSrc(null);
        }
      });

    return () => {
      controller.abort();
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [userId, hasPhoto, revision]);

  const initials = initialsFor(name, email);
  return (
    <span className={`user-avatar user-avatar-${size}`} aria-hidden="true">
      {src ? <img src={src} alt="" /> : initials}
    </span>
  );
}
