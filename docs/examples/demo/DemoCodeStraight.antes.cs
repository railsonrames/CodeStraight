using System.Text;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Tienda.Demo
{
    // Demostración de CodeStraight: guarda este archivo (Ctrl+S) y mira la ventana Salida -> CodeStraight.
    public interface DemoRepositorio
    {
        Task<int> ContarPedidos(int proyecto);
    }

    class DemoServicio : DemoRepositorio
    {
        private const string mensaje_base = "Resultado del cálculo para el año en curso en España";
        private int contador;
        private readonly List<string> nombres = new List<string>();



        public async Task<int> ContarPedidos(int proyecto)
        {
            await Task.Delay(1);
            contador++;
            return contador + proyecto;
        }

        public async Task<string> ObtenerResumenDeLaSolicitudConTodosLosDatos(int proyectoSeleccionado, string nombreDelCliente, string codigoDeLaRegion, bool incluirHistorico)
        {
            var total = await ContarPedidos(
                proyectoSeleccionado,
            );
            var lista = nombres
.Where(n => n.StartsWith("ñ"))
.Select(n => n.ToUpper())
.ToList();
            var resumen = new Dictionary<string, int>
            {
                ["pedidos"] = total,
                ["nombres"] = lista.Count,
            };
            string texto = mensaje_base + nombreDelCliente + "Este texto es deliberadamente muy largo para superar el límite de ciento veinte caracteres por línea del editor y así ver cómo se parte en varias piezas.";
            string aviso =
 "Revisión pendiente: "
  +

 "acción requerida por el usuario, ¿confirmar?";
            var constructor = new StringBuilder(texto.ToUpper(CultureInfo.InvariantCulture));
            return constructor + aviso + resumen.Count + codigoDeLaRegion + incluirHistorico;
        }


    }
}
