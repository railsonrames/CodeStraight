using System.Globalization;
using System.Text;

namespace Tienda.Demo;

// Demostración de CodeStraight: guarda este archivo (Ctrl+S) y mira la ventana Salida -> CodeStraight.
public interface IDemoRepositorio
{
	Task<int> ContarPedidosAsync(int proyecto);
}

internal class DemoServicio : IDemoRepositorio
{
	private const string MensajeBase = "Resultado del cálculo para el año en curso en España";
	private int _contador;
	private readonly List<string> _nombres = new List<string>();

	public async Task<int> ContarPedidosAsync(int proyecto)
	{
		await Task.Delay(1);
		_contador++;
		return _contador + proyecto;
	}

	public async Task<string> ObtenerResumenDeLaSolicitudConTodosLosDatosAsync(
		int proyectoSeleccionado,
		string nombreDelCliente,
		string codigoDeLaRegion,
		bool incluirHistorico)
	{
		var total = await ContarPedidosAsync(
			proyectoSeleccionado
		);
		var lista = _nombres
			.Where(n => n.StartsWith("ñ"))
			.Select(n => n.ToUpper())
			.ToList();
		var resumen = new Dictionary<string, int>
		{
			["pedidos"] = total,
			["nombres"] = lista.Count
		};
		string texto = MensajeBase
			+ nombreDelCliente
			+ "Este texto es deliberadamente muy largo para superar el límite de ciento veinte caracteres por línea "
			+ "del editor y así ver cómo se parte en varias piezas.";
		string aviso =
			"Revisión pendiente: "
				+ "acción requerida por el usuario, ¿confirmar?";
		var constructor = new StringBuilder(texto.ToUpper(CultureInfo.InvariantCulture));
		return constructor + aviso + resumen.Count + codigoDeLaRegion + incluirHistorico;
	}
}
