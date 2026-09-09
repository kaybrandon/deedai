import { FormEvent, useState } from "react";
import { endpoints, type ApiError, type ClientScope } from "../api";
import { useAuth } from "../auth";
import EmptyState from "../components/EmptyState";
import PasswordPair, { passwordPairErrors } from "../components/PasswordPair";
import PhotoEditor from "../components/PhotoEditor";

export default function ProfilePage() {
  const { me, refreshMe } = useAuth();
  const [displayName, setDisplayName] = useState(me?.displayName ?? "");
  const [fullName, setFullName] = useState(me?.fullName ?? "");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [confirmError, setConfirmError] = useState<string | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const [hasPhoto, setHasPhoto] = useState(Boolean(me?.hasPhoto));

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    const next = passwordPairErrors(password, confirm, false);
    setPasswordError(next.password);
    setConfirmError(next.confirm);
    if (next.password || next.confirm) {
      return;
    }

    setError(null);
    try {
      const saved = await endpoints.updateProfile({
        displayName,
        fullName: fullName.trim() || null,
        password: password || null
      });
      setPassword("");
      setConfirm("");
      setHasPhoto(saved.hasPhoto);
      setNotice(password ? "Profile and password updated." : "Profile updated.");
      await refreshMe();
    } catch (err) {
      const apiError = err as ApiError;
      if (apiError.field === "password") {
        setPasswordError(apiError.message);
      } else {
        setError(apiError.message ?? "Save failed.");
      }
    }
  }

  return (
    <section className="page">
      <div className="page-head">
        <div>
          <h1>My Profile</h1>
          <p className="page-kicker">Your name, photo, password, and assigned Client(s).</p>
        </div>
      </div>
      {notice && <div className="success-banner">{notice}</div>}
      {error && <div className="denied-box">{error}</div>}
      <AssignedClients clients={me?.clients ?? []} />
      <form className="panel compact-form" onSubmit={onSubmit}>
        <PhotoEditor
          userId={me?.id}
          name={displayName || me?.email}
          email={me?.email}
          hasPhoto={hasPhoto}
          revision={revision}
          error={photoError}
          onUpload={async (file) => {
            setPhotoError(null);
            try {
              const saved = await endpoints.uploadMyPhoto(file);
              setHasPhoto(saved.hasPhoto);
              setRevision((value) => value + 1);
              setNotice("Photo updated.");
              await refreshMe();
            } catch (err) {
              setPhotoError(err instanceof Error ? err.message : "Photo upload failed.");
            }
          }}
          onClear={async () => {
            setPhotoError(null);
            try {
              const saved = await endpoints.clearMyPhoto();
              setHasPhoto(saved.hasPhoto);
              setRevision((value) => value + 1);
              setNotice("Photo removed.");
              await refreshMe();
            } catch (err) {
              setPhotoError(err instanceof Error ? err.message : "Could not remove photo.");
            }
          }}
        />
        <div className="form-grid">
          <label>
            Display Name
            <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} required />
          </label>
          <label>
            Full Name
            <input value={fullName} onChange={(e) => setFullName(e.target.value)} />
          </label>
          <PasswordPair
            id="profile-password"
            passwordLabel="New Password (Optional)"
            password={password}
            confirm={confirm}
            passwordError={passwordError}
            confirmError={confirmError}
            onPassword={(value) => {
              setPassword(value);
              if (passwordError) setPasswordError(null);
            }}
            onConfirm={(value) => {
              setConfirm(value);
              if (confirmError) setConfirmError(null);
            }}
          />
        </div>
        <div className="row-actions">
          <button className="primary" type="submit">
            Save
          </button>
        </div>
      </form>
    </section>
  );
}

function AssignedClients({ clients }: { clients: ClientScope[] }) {
  return (
    <section className="panel compact-form profile-clients" aria-labelledby="profile-clients-heading">
      <h2 id="profile-clients-heading">Assigned Client(s)</h2>
      {clients.length === 0 ? (
        <EmptyState
          title="No Client assigned"
          body="An Admin can assign Client access on Users. You will only see deeds for Clients you are assigned."
        />
      ) : (
        <ul className="profile-client-list">
          {clients.map((client) => (
            <li key={client.id} className="profile-client-chip">
              {client.name}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
