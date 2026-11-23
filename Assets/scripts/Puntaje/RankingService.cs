using System;
using System.Collections.Generic;
using UnityEngine;

// Un puntaje guardado. Los campos 'id', 'jugadorId' y 'version' existen pensando en un ranking mundial futuro.
[Serializable]
public class RegistroPuntaje
{
    public string id;          // identificador unico del registro
    public string jugadorId;   // identificador anonimo de este dispositivo/jugador
    public string nombre;
    public int nivel;
    public int puntaje;
    public int estrellas;
    public long fecha;         // segundos Unix (UTC)
    public string version;     // version del juego
}

[Serializable]
public class ListaRegistros
{
    public List<RegistroPuntaje> items = new List<RegistroPuntaje>();
}

// Contrato del almacenamiento. Hoy: RankingLocal (en el dispositivo).
// Manana: crear RankingOnline : IRankingBackend (con tu servidor o un servicio como Unity Leaderboards)
// y asignar  RankingService.Backend = new RankingOnline();  el resto del juego no cambia.
// Los metodos usan callbacks porque un servidor responde de forma asincrona.
public interface IRankingBackend
{
    void Guardar(RegistroPuntaje registro, Action<bool> alTerminar);
    void Obtener(int nivel, int maximo, Action<List<RegistroPuntaje>> alTerminar);
}

// Ranking en el dispositivo (PlayerPrefs + JSON).
public class RankingLocal : IRankingBackend
{
    private static string Clave(int nivel) { return "Ranking_Nivel" + nivel; }

    public void Guardar(RegistroPuntaje registro, Action<bool> alTerminar)
    {
        ListaRegistros lista = Cargar(registro.nivel);
        lista.items.Add(registro);
        Ordenar(lista.items);

        int max = Mathf.Max(5, PuntajeConfig.Actual.maxRegistrosPorNivel);
        if (lista.items.Count > max) lista.items.RemoveRange(max, lista.items.Count - max);

        PlayerPrefs.SetString(Clave(registro.nivel), JsonUtility.ToJson(lista));
        PlayerPrefs.Save();
        if (alTerminar != null) alTerminar(true);
    }

    public void Obtener(int nivel, int maximo, Action<List<RegistroPuntaje>> alTerminar)
    {
        ListaRegistros lista = Cargar(nivel);
        Ordenar(lista.items);
        if (maximo > 0 && lista.items.Count > maximo) lista.items.RemoveRange(maximo, lista.items.Count - maximo);
        if (alTerminar != null) alTerminar(lista.items);
    }

    private static ListaRegistros Cargar(int nivel)
    {
        string json = PlayerPrefs.GetString(Clave(nivel), "");
        if (string.IsNullOrEmpty(json)) return new ListaRegistros();
        try
        {
            ListaRegistros l = JsonUtility.FromJson<ListaRegistros>(json);
            return l ?? new ListaRegistros();
        }
        catch (Exception)
        {
            return new ListaRegistros();   // datos danados: se empieza de cero
        }
    }

    // Mayor puntaje primero; si empatan, el mas antiguo
    private static void Ordenar(List<RegistroPuntaje> l)
    {
        l.Sort((a, b) =>
        {
            int c = b.puntaje.CompareTo(a.puntaje);
            return c != 0 ? c : a.fecha.CompareTo(b.fecha);
        });
    }
}

// Punto de acceso unico al ranking.
public static class RankingService
{
    private static IRankingBackend backend;

    public static IRankingBackend Backend
    {
        get { if (backend == null) backend = new RankingLocal(); return backend; }
        set { backend = value; }
    }

    // Identificador anonimo y estable de este jugador (util cuando exista un ranking mundial)
    public static string JugadorId
    {
        get
        {
            string id = PlayerPrefs.GetString("Ranking_JugadorId", "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString("Ranking_JugadorId", id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    public static string UltimoNombre
    {
        get { return PlayerPrefs.GetString("Ranking_UltimoNombre", ""); }
        set { PlayerPrefs.SetString("Ranking_UltimoNombre", value ?? ""); PlayerPrefs.Save(); }
    }

    // Mejor puntaje personal de este dispositivo (para el cartel de "nuevo record")
    public static int MejorPersonal(int nivel)
    {
        return PlayerPrefs.GetInt("Ranking_MejorPersonal" + nivel, 0);
    }

    public static void RegistrarIntento(int nivel, int puntaje)
    {
        if (puntaje > MejorPersonal(nivel))
        {
            PlayerPrefs.SetInt("Ranking_MejorPersonal" + nivel, puntaje);
            PlayerPrefs.Save();
        }
    }

    public static RegistroPuntaje Nuevo(string nombre, int nivel, int puntaje, int estrellas)
    {
        nombre = string.IsNullOrWhiteSpace(nombre) ? "Jugador" : nombre.Trim();
        return new RegistroPuntaje
        {
            id = Guid.NewGuid().ToString("N"),
            jugadorId = JugadorId,
            nombre = nombre,
            nivel = nivel,
            puntaje = puntaje,
            estrellas = estrellas,
            fecha = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            version = Application.version
        };
    }

    public static void Guardar(RegistroPuntaje r, Action<bool> alTerminar)
    {
        UltimoNombre = r.nombre;
        Backend.Guardar(r, alTerminar);
    }

    public static void Obtener(int nivel, int maximo, Action<List<RegistroPuntaje>> alTerminar)
    {
        Backend.Obtener(nivel, maximo, alTerminar);
    }

    // Solo para pruebas: borra todos los puntajes de este dispositivo
    public static void BorrarTodoLocal()
    {
        for (int n = 1; n <= 3; n++)
        {
            PlayerPrefs.DeleteKey("Ranking_Nivel" + n);
            PlayerPrefs.DeleteKey("Ranking_MejorPersonal" + n);
        }
        PlayerPrefs.Save();
    }
}
