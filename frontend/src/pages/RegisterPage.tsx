import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { TextField } from '../components/forms/TextField';
import { Button } from '../components/forms/Button';
import { ErrorText } from '../components/forms/ErrorText';

export default function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await register(email, password, displayName);
      navigate('/');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Une erreur est survenue.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-canvas">
      <form onSubmit={handleSubmit} className="bg-white border border-line rounded-lg shadow-sm p-8 w-full max-w-sm space-y-4">
        <div className="flex items-center gap-2">
          <span className="w-2 h-2 rounded-full bg-primary-600" aria-hidden="true" />
          <h1 className="text-lg font-semibold text-ink">Inscription</h1>
        </div>
        <ErrorText message={error} />
        <TextField label="Nom affiché" required value={displayName} onChange={setDisplayName} />
        <TextField label="Email" type="email" required value={email} onChange={setEmail} />
        <TextField label="Mot de passe (8 caractères min.)" type="password" required value={password} onChange={setPassword} />
        <Button type="submit" disabled={submitting} className="w-full">
          {submitting ? 'Inscription...' : "S'inscrire"}
        </Button>
        <p className="text-sm text-gray-600 text-center">
          Déjà un compte ? <Link to="/login" className="text-primary-600 hover:text-primary-700 underline">Se connecter</Link>
        </p>
      </form>
    </div>
  );
}
